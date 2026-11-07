namespace Sentium.Core.Constants;

public static class EnvConfig
{
    public static class Keys
    {
        public const string DockerHost = "DOCKER_HOST";

        /// <summary>
        /// Pre-shared key between Sentium.Web and the separate Sentium.Sandbox, sent as the
        /// <c>X-Internal-Token</c> header. Maps to <c>InternalApi:ApiKey</c> in configuration.
        /// </summary>
        public const string InternalApiKey = "InternalApi__ApiKey";

        /// <summary>
        /// The <c>Inference</c> configuration section (Sentium.Infrastructure's <c>InferenceOptions</c>)
        /// plus the RAG embedding model name.
        /// </summary>
        public static class Inference
        {
            public const string ChatEndpoint = "Inference__ChatEndpoint";
            public const string EmbeddingEndpoint = "Inference__EmbeddingEndpoint";
            public const string DefaultModel = "Inference__DefaultModel";
            public const string ApiKey = "Inference__ApiKey";
            public const string EmbeddingModelName = "Rag__EmbeddingModelName";
            public const string ServerApiKey = "LLAMA_API_KEY";
        }

        public static class Frontend
        {
            public const string ApiProxyTarget = "API_PROXY_TARGET";
        }
    }

    public static class Values
    {
        public const string True = "1";

        public static class DockerSockets
        {
            public const string Windows = "npipe://./pipe/docker_engine";
            public const string LinuxMac = "unix:///var/run/docker.sock";
        }
    }
}
