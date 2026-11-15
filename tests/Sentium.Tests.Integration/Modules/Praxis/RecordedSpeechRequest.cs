namespace Sentium.Tests.Integration.Modules.Praxis;

public sealed record RecordedSpeechRequest(HttpMethod Method, Uri Uri, string Body);
