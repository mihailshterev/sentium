using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Sentium.Infrastructure.Security;

namespace Sentium.Praxis.Features.Speech;

/// <summary>
/// Push-to-talk and spoken replies. The browser talks only to these endpoints, so audio stays on the home server
/// and the speech containers stay off the LAN. A recording is transcribed and discarded: only the transcript goes
/// back, and it enters a conversation the same way a typed message does.
/// </summary>
[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/speech")]
internal sealed class SpeechController(ISpeechService speech, SpeechRateLimiter rateLimiter, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Room for the multipart envelope around a maximum-size recording.
    /// </summary>
    private const long MaxRequestBytes = SpeechLimits.MaxAudioBytes + (64 * 1024);

    /// <summary>
    /// Reports whether transcription and synthesis are usable and lists the Voices. Always answers 200: a
    /// deployment without speech containers is a normal state.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SpeechStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        return Ok(await speech.GetStatusAsync(ct));
    }

    /// <summary>
    /// Transcribes one recording (multipart field <c>file</c>; WebM/Opus, MP4/AAC, WAV or MP3).
    /// </summary>
    /// <response code="400">No recording, or it is too large.</response>
    /// <response code="422">The recording could not be read as audio.</response>
    /// <response code="429">The caller's transcription budget for this minute is spent.</response>
    /// <response code="503">Speech-to-text is switched off or its server is down.</response>
    [HttpPost("transcribe")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType(typeof(TranscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Transcribe(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(detail: "Send the recording as the multipart field 'file'.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > SpeechLimits.MaxAudioBytes)
        {
            return Problem(detail: "The recording is too long.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        if (!rateLimiter.TryAcquireTranscription(userId))
        {
            return TooManyRequests();
        }

        await using var audio = file.OpenReadStream();
        var result = await speech.TranscribeAsync(audio, ct);

        return result.Outcome switch
        {
            SpeechOutcome.Success => Ok(new TranscriptionResponse(result.Value!)),
            SpeechOutcome.Failed => Problem(detail: "The recording could not be transcribed.", statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Unavailable()
        };
    }

    /// <summary>
    /// Synthesises one short piece of text and returns it as MP3.
    /// </summary>
    /// <response code="422">The speech server could not synthesise the text (for example an unknown Voice).</response>
    /// <response code="429">The caller's synthesis budget for this minute is spent.</response>
    /// <response code="503">Text-to-speech is switched off or its server is down.</response>
    [HttpPost("speak")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Speak([FromBody] SpeakRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        if (!rateLimiter.TryAcquireSynthesis(userId))
        {
            return TooManyRequests();
        }

        var result = await speech.SynthesizeAsync(request.Text, request.Voice, request.Speed, ct);
        if (result.Outcome == SpeechOutcome.Success)
        {
            // Spoken answers are private: keep them out of any shared or disk cache.
            Response.Headers[HeaderNames.CacheControl] = "no-store";
            return File(result.Value!.Data.ToArray(), result.Value.ContentType);
        }

        return result.Outcome == SpeechOutcome.Failed
            ? Problem(detail: "The text could not be spoken.", statusCode: StatusCodes.Status422UnprocessableEntity)
            : Unavailable();
    }

    private ObjectResult Unavailable() => Problem(detail: "Speech is not available right now.", statusCode: StatusCodes.Status503ServiceUnavailable);

    private ObjectResult TooManyRequests() => Problem(detail: "Too many speech requests. Try again in a minute.", statusCode: StatusCodes.Status429TooManyRequests);
}
