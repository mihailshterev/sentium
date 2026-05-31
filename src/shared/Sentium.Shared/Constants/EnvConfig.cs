namespace Sentium.Shared.Constants;

public static class EnvConfig
{
    public static class Keys
    {
        public const string IdentityAuthority = "Identity__Authority";
        public const string DockerHost = "DOCKER_HOST";
        public const string AcceptEula = "ACCEPT_EULA";

        /// <summary>
        /// Injects the pre-shared internal API key into all services so they can authenticate
        /// service-to-service calls via the <c>X-Internal-Token</c> header.
        /// Maps to <c>InternalApi:ApiKey</c> in service configuration.
        /// </summary>
        public const string InternalApiKey = "InternalApi__ApiKey";

        public static class AI
        {
            public const string ModelName = "AI__ModelName";
            public const string EmbeddingModelName = "Rag__EmbeddingModelName";
        }

        public static class Frontend
        {
            public const string ViteApiBase = "VITE_API_BASE";
            public const string ViteIdentityApiBase = "VITE_IDENTITY_API_BASE";
        }
    }

    public static class Values
    {
        public const string True = "1";
        public const string Yes = "Y";

        public static class DockerSockets
        {
            public const string Windows = "npipe://./pipe/docker_engine";
            public const string LinuxMac = "unix:///var/run/docker.sock";
        }
    }
}
