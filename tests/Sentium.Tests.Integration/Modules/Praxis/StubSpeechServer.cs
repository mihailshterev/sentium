using System.Net;
using System.Text;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// Answers like the speech containers do: whisper.cpp's server for transcriptions, Kokoro-FastAPI for voices and
/// speech, and <c>/health</c> on both. The response shapes mirror what the pinned images return.
/// </summary>
public sealed class StubSpeechServer : HttpMessageHandler
{
    public static readonly byte[] Audio = Encoding.ASCII.GetBytes("ID3-stub-mp3-audio");

    private readonly Lock _gate = new();
    private readonly List<RecordedSpeechRequest> _requests = [];

    /// <summary>
    /// <see langword="false"/> makes every request fail to connect, like a stopped container.
    /// </summary>
    public bool IsUp { get; set; } = true;

    public string Transcript { get; set; } = " The drill is in the garage on the second shelf.\n";

    /// <summary>
    /// When set, transcription answers with whisper.cpp's conversion error for unreadable audio.
    /// </summary>
    public bool RejectRecordings { get; set; }

    public IReadOnlyList<RecordedSpeechRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    public void Reset()
    {
        IsUp = true;
        RejectRecordings = false;
        lock (_gate)
        {
            _requests.Clear();
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsUp)
        {
            throw new HttpRequestException("Connection refused");
        }

        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        lock (_gate)
        {
            _requests.Add(new RecordedSpeechRequest(request.Method, request.RequestUri, body));
        }

        return path switch
        {
            "/health" => Json(HttpStatusCode.OK, """{"status":"ok"}"""),
            "/v1/audio/voices" => Json(HttpStatusCode.OK, """{"voices":[{"id":"am_adam","name":"am_adam"},{"id":"af_heart","name":"af_heart","overall_grade":"A"}]}"""),
            "/v1/audio/transcriptions" when RejectRecordings => Json(HttpStatusCode.InternalServerError, """{"error":"FFmpeg conversion failed."}"""),
            "/v1/audio/transcriptions" => Json(HttpStatusCode.OK, $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(Transcript)}}}"""),
            "/v1/audio/speech" => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Audio) { Headers = { ContentType = new("audio/mpeg") } }
            },
            _ => Json(HttpStatusCode.NotFound, "{}")
        };
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
