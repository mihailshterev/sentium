using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Speech;
using Sentium.Praxis.Features.Speech;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Speech;

public sealed class SpeechControllerTests : IDisposable
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly ISpeechService _speech = Substitute.For<ISpeechService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly SpeechRateLimiter _rateLimiter = new(Options.Create(new SpeechOptions { TranscriptionsPerMinute = 2, SynthesesPerMinute = 2 }));

    public SpeechControllerTests()
    {
        _currentUser.UserId.Returns(UserId);
    }

    public void Dispose() => _rateLimiter.Dispose();

    private SpeechController Controller()
    {
        return new SpeechController(_speech, _rateLimiter, _currentUser)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static FormFile Recording(int length = 16)
    {
        return new FormFile(new MemoryStream(new byte[Math.Min(length, 16)]), 0, length, "file", "speech.webm");
    }

    private static int? StatusOf(IActionResult result) => (result as ObjectResult)?.StatusCode;

    [Fact]
    public async Task GetStatus_ReturnsWhatTheServiceReports()
    {
        var status = new SpeechStatusResponse(true, false, "af_heart", [], SpeechLimits.MaxTextLength);
        _speech.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var result = await Controller().GetStatus(Ct);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(status);
    }

    [Fact]
    public async Task Transcribe_ReturnsTheTranscript()
    {
        _speech.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<string>.Success("where is the drill?"));

        var result = await Controller().Transcribe(Recording(), Ct);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(new TranscriptionResponse("where is the drill?"));
    }

    [Fact]
    public async Task Transcribe_Returns400_WithoutARecording()
    {
        StatusOf(await Controller().Transcribe(null, Ct)).Should().Be(StatusCodes.Status400BadRequest);
        StatusOf(await Controller().Transcribe(Recording(length: 0), Ct)).Should().Be(StatusCodes.Status400BadRequest);
        await _speech.DidNotReceive().TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transcribe_Returns400_WhenTheRecordingIsTooLarge()
    {
        var result = await Controller().Transcribe(Recording((int)SpeechLimits.MaxAudioBytes + 1), Ct);

        StatusOf(result).Should().Be(StatusCodes.Status400BadRequest);
        await _speech.DidNotReceive().TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transcribe_Returns503_WhenSpeechIsUnavailable()
    {
        _speech.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<string>.Unavailable);

        StatusOf(await Controller().Transcribe(Recording(), Ct)).Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Transcribe_Returns422_WhenTheRecordingCannotBeRead()
    {
        _speech.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<string>.Failed);

        StatusOf(await Controller().Transcribe(Recording(), Ct)).Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task Transcribe_Returns429_OnceTheUsersBudgetIsSpent_WithoutCallingTheServer()
    {
        _speech.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<string>.Success("ok"));
        var controller = Controller();

        await controller.Transcribe(Recording(), Ct);
        await controller.Transcribe(Recording(), Ct);
        var third = await controller.Transcribe(Recording(), Ct);

        StatusOf(third).Should().Be(StatusCodes.Status429TooManyRequests);
        await _speech.Received(2).TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transcribe_BudgetsAreSeparatePerUser()
    {
        _speech.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<string>.Success("ok"));
        var controller = Controller();
        await controller.Transcribe(Recording(), Ct);
        await controller.Transcribe(Recording(), Ct);

        _currentUser.UserId.Returns(Guid.NewGuid());
        var otherUser = await controller.Transcribe(Recording(), Ct);

        otherUser.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Speak_ReturnsTheAudio_AndForbidsCaching()
    {
        var audio = Encoding.ASCII.GetBytes("ID3-fake-mp3");
        _speech.SynthesizeAsync("Hello.", "af_heart", 1.2f, Arg.Any<CancellationToken>())
            .Returns(SpeechResult<SpeechAudio>.Success(new SpeechAudio(audio, "audio/mpeg")));
        var controller = Controller();

        var result = await controller.Speak(new SpeakRequest("Hello.", "af_heart", 1.2f), Ct);

        var file = result.Should().BeOfType<FileContentResult>().Which;
        file.ContentType.Should().Be("audio/mpeg");
        file.FileContents.Should().Equal(audio);
        controller.Response.Headers[HeaderNames.CacheControl].ToString().Should().Be("no-store");
    }

    [Fact]
    public async Task Speak_Returns503_WhenSpeechIsUnavailable()
    {
        _speech.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<float?>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<SpeechAudio>.Unavailable);

        StatusOf(await Controller().Speak(new SpeakRequest("Hello."), Ct)).Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Speak_Returns422_WhenTheServerRejectsTheText()
    {
        _speech.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<float?>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<SpeechAudio>.Failed);

        StatusOf(await Controller().Speak(new SpeakRequest("Hello.", "no_such_voice"), Ct)).Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task Speak_Returns429_OnceTheUsersBudgetIsSpent()
    {
        _speech.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<float?>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<SpeechAudio>.Unavailable);
        var controller = Controller();

        await controller.Speak(new SpeakRequest("One."), Ct);
        await controller.Speak(new SpeakRequest("Two."), Ct);

        StatusOf(await controller.Speak(new SpeakRequest("Three."), Ct)).Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Endpoints_Return401_WithoutAUserId()
    {
        _currentUser.UserId.Returns((Guid?)null);

        (await Controller().Transcribe(Recording(), Ct)).Should().BeOfType<UnauthorizedResult>();
        (await Controller().Speak(new SpeakRequest("Hello."), Ct)).Should().BeOfType<UnauthorizedResult>();
    }
}
