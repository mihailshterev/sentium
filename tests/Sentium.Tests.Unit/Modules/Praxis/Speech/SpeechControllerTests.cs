using System.Text;
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

        Assert.Equal(status, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Transcribe_ReturnsTheTranscript()
    {
        _speech.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<string>.Success("where is the drill?"));

        var result = await Controller().Transcribe(Recording(), Ct);

        Assert.Equal(new TranscriptionResponse("where is the drill?"), Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Transcribe_Returns400_WithoutARecording()
    {
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(await Controller().Transcribe(null, Ct)));
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(await Controller().Transcribe(Recording(length: 0), Ct)));
        await _speech.DidNotReceive().TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transcribe_Returns400_WhenTheRecordingIsTooLarge()
    {
        var result = await Controller().Transcribe(Recording((int)SpeechLimits.MaxAudioBytes + 1), Ct);

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
        await _speech.DidNotReceive().TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transcribe_Returns503_WhenSpeechIsUnavailable()
    {
        _speech.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<string>.Unavailable);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, StatusOf(await Controller().Transcribe(Recording(), Ct)));
    }

    [Fact]
    public async Task Transcribe_Returns422_WhenTheRecordingCannotBeRead()
    {
        _speech.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<string>.Failed);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusOf(await Controller().Transcribe(Recording(), Ct)));
    }

    [Fact]
    public async Task Transcribe_Returns429_OnceTheUsersBudgetIsSpent_WithoutCallingTheServer()
    {
        _speech.TranscribeAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<string>.Success("ok"));
        var controller = Controller();

        await controller.Transcribe(Recording(), Ct);
        await controller.Transcribe(Recording(), Ct);
        var third = await controller.Transcribe(Recording(), Ct);

        Assert.Equal(StatusCodes.Status429TooManyRequests, StatusOf(third));
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

        Assert.IsType<OkObjectResult>(otherUser);
    }

    [Fact]
    public async Task Speak_ReturnsTheAudio_AndForbidsCaching()
    {
        var audio = Encoding.ASCII.GetBytes("ID3-fake-mp3");
        _speech.SynthesizeAsync("Hello.", "af_heart", 1.2f, Arg.Any<CancellationToken>())
            .Returns(SpeechResult<SpeechAudio>.Success(new SpeechAudio(audio, "audio/mpeg")));
        var controller = Controller();

        var result = await controller.Speak(new SpeakRequest("Hello.", "af_heart", 1.2f), Ct);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("audio/mpeg", file.ContentType);
        Assert.Equal(audio, file.FileContents);
        Assert.Equal("no-store", controller.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public async Task Speak_Returns503_WhenSpeechIsUnavailable()
    {
        _speech.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<float?>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<SpeechAudio>.Unavailable);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, StatusOf(await Controller().Speak(new SpeakRequest("Hello."), Ct)));
    }

    [Fact]
    public async Task Speak_Returns422_WhenTheServerRejectsTheText()
    {
        _speech.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<float?>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<SpeechAudio>.Failed);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusOf(await Controller().Speak(new SpeakRequest("Hello.", "no_such_voice"), Ct)));
    }

    [Fact]
    public async Task Speak_Returns429_OnceTheUsersBudgetIsSpent()
    {
        _speech.SynthesizeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<float?>(), Arg.Any<CancellationToken>()).Returns(SpeechResult<SpeechAudio>.Unavailable);
        var controller = Controller();

        await controller.Speak(new SpeakRequest("One."), Ct);
        await controller.Speak(new SpeakRequest("Two."), Ct);

        Assert.Equal(StatusCodes.Status429TooManyRequests, StatusOf(await controller.Speak(new SpeakRequest("Three."), Ct)));
    }

    [Fact]
    public async Task Endpoints_Return401_WithoutAUserId()
    {
        _currentUser.UserId.Returns((Guid?)null);

        Assert.IsType<UnauthorizedResult>(await Controller().Transcribe(Recording(), Ct));
        Assert.IsType<UnauthorizedResult>(await Controller().Speak(new SpeakRequest("Hello."), Ct));
    }
}
