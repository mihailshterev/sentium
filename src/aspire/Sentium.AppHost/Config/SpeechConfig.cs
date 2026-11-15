namespace Sentium.AppHost.Config;

/// <summary>
/// Speech containers for the Companion's voice: whisper.cpp's server (speech-to-text) and Kokoro-FastAPI
/// (text-to-speech). Both run on the CPU so they never compete with the chat model for GPU memory.
/// Mirrors <c>docker-compose.yml</c>.
/// </summary>
internal static class SpeechConfig
{
    /// <summary>
    /// Set to <c>false</c> (e.g. <c>Speech__Enabled=false</c> or user secrets) to run without the speech
    /// containers; the portal then hides its voice controls.
    /// </summary>
    public const string EnabledKey = "Speech:Enabled";

    public const string WhisperRegistry = "ghcr.io";
    public const string WhisperImage = "ggml-org/whisper.cpp";

    // Pinned by digest. After an upgrade, re-check transcription of WebM/Opus and MP4/AAC uploads (ffmpeg
    // conversion) and the /health endpoint.
    public const string WhisperTag = "main-1fe009caeda75f69bc864d6370b10674e45a92bd";
    public const string WhisperDigest = "77926a46acc8aafab4af8fa932daf8a2df22151954257e18b6a38911ed9f02fc";

    /// <summary>
    /// The image's entrypoint is <c>bash -c</c>; the server binary is started directly instead.
    /// </summary>
    public const string WhisperServerPath = "/app/build/bin/whisper-server";
    public const string WhisperModelFile = "ggml-base.en.bin";

    /// <summary>
    /// Silero voice-activity model. With it a recording without speech transcribes to nothing; without it
    /// Whisper invents a word ("you") for silence.
    /// </summary>
    public const string WhisperVadModelFile = "ggml-silero-v6.2.0.bin";
    public const int WhisperContainerPort = 8080;
    public const int WhisperHostPort = 8092;

    /// <summary>
    /// The path the OpenAI-compatible clients post recordings to; whisper-server's own default is <c>/inference</c>.
    /// </summary>
    public const string WhisperInferencePath = "/v1/audio/transcriptions";

    public const string TtsRegistry = "ghcr.io";
    public const string TtsImage = "remsky/kokoro-fastapi-cpu";

    // The Kokoro model and voices are baked into the image, so the digest pins them too.
    public const string TtsTag = "v0.10.0";
    public const string TtsDigest = "f54c5960c2b9387f6349080a9944f8bc7e8cdb98a4ef9238cb0b0f9ff9795f44";
    public const int TtsContainerPort = 8880;
    public const int TtsHostPort = 8093;
}
