using System;
using UnityEngine;

namespace UIU.Simulator.Networking
{
    /// <summary>
    /// Runtime-safe backend URL configuration baked into the player at build time.
    /// Values are synced from <c>backend/.env</c> by editor tooling only — the standalone
    /// player never reads the repository .env file.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BackendConfig",
        menuName = "UIU Simulator/Networking/Backend Config")]
    public sealed class BackendConfig : ScriptableObject
    {
        public const string DefaultResourcePath = "BackendConfig";
        public const string RemotePlaceholder = "__RENDER_BACKEND_URL_NOT_CONFIGURED__";

        public const string EnvModeKey = "UIU_BACKEND_MODE";
        public const string EnvLocalUrlKey = "UIU_LOCAL_BACKEND_URL";
        public const string EnvRemoteUrlKey = "UIU_REMOTE_BACKEND_URL";

        [SerializeField] private BackendEnvironment environment = BackendEnvironment.LOCAL;
        [SerializeField] private string localBackendUrl = "http://localhost:8080";
        [SerializeField] private string remoteBackendUrl = RemotePlaceholder;

        private static BackendConfig cached;
        private static bool startupLogged;

        public BackendEnvironment Environment => environment;

        public string LocalBackendUrl => NormalizeBaseUrl(localBackendUrl);

        public string RemoteBackendUrl => NormalizeBaseUrl(remoteBackendUrl);

        /// <summary>
        /// Resolved API base URL for the selected environment (no trailing slash).
        /// Empty when configuration is invalid — callers must treat that as a hard failure.
        /// </summary>
        public string BaseUrl
        {
            get
            {
                if (!TryResolveBaseUrl(out string url, out string error))
                {
                    Debug.LogError($"[BackendConfig] {error}");
                    return string.Empty;
                }

                return url;
            }
        }

        public static BackendConfig Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<BackendConfig>(DefaultResourcePath);
            }

            return cached;
        }

        /// <summary>
        /// Loads the Resources asset and returns the resolved base URL.
        /// Logs environment + URL once per play/session (safe — URL is public).
        /// </summary>
        public static string ResolveBaseUrl()
        {
            BackendConfig config = Load();
            if (config == null)
            {
                Debug.LogError(
                    "[BackendConfig] Missing Resources/BackendConfig asset. " +
                    "Run menu: UIU Simulator → Sync Backend Config From .env");
                return string.Empty;
            }

            if (!config.TryResolveBaseUrl(out string url, out string error))
            {
                Debug.LogError($"[BackendConfig] {error}");
                return string.Empty;
            }

            if (!startupLogged)
            {
                Debug.Log($"[BackendConfig] Environment: {config.environment}");
                Debug.Log($"[BackendConfig] Base URL: {url}");
                startupLogged = true;
            }

            return url;
        }

        public bool TryResolveBaseUrl(out string baseUrl, out string error)
        {
            baseUrl = string.Empty;
            error = null;

            switch (environment)
            {
                case BackendEnvironment.LOCAL:
                    return ValidateLocalUrl(localBackendUrl, out baseUrl, out error);

                case BackendEnvironment.REMOTE:
                    return ValidateRemoteUrl(remoteBackendUrl, out baseUrl, out error);

                default:
                    error = $"Unknown BackendEnvironment value: {environment}";
                    return false;
            }
        }

        public void ApplyFromEnv(BackendEnvironment mode, string localUrl, string remoteUrl)
        {
            environment = mode;
            if (!string.IsNullOrWhiteSpace(localUrl))
            {
                localBackendUrl = localUrl.Trim();
            }

            if (!string.IsNullOrWhiteSpace(remoteUrl))
            {
                remoteBackendUrl = remoteUrl.Trim();
            }
        }

        public static bool TryParseEnvironment(string raw, out BackendEnvironment mode)
        {
            mode = BackendEnvironment.LOCAL;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return Enum.TryParse(raw.Trim(), ignoreCase: true, out mode);
        }

        public static bool ValidateLocalUrl(string raw, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "LOCAL backend selected but UIU_LOCAL_BACKEND_URL is empty.";
                return false;
            }

            normalized = NormalizeBaseUrl(raw);
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = $"LOCAL backend URL is malformed: '{raw}'";
                normalized = string.Empty;
                return false;
            }

            return true;
        }

        public static bool ValidateRemoteUrl(string raw, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "REMOTE backend selected but UIU_REMOTE_BACKEND_URL has not been configured.";
                return false;
            }

            string trimmed = raw.Trim();
            if (string.Equals(trimmed, RemotePlaceholder, StringComparison.Ordinal) ||
                trimmed.Contains("NOT_CONFIGURED", StringComparison.OrdinalIgnoreCase))
            {
                error = "REMOTE backend selected but UIU_REMOTE_BACKEND_URL has not been configured.";
                return false;
            }

            normalized = NormalizeBaseUrl(trimmed);
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri))
            {
                error = $"REMOTE backend URL is malformed: '{raw}'";
                normalized = string.Empty;
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "REMOTE backend selected but UIU_REMOTE_BACKEND_URL must be HTTPS " +
                    $"(got '{uri.Scheme}').";
                normalized = string.Empty;
                return false;
            }

            return true;
        }

        public static string NormalizeBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            return url.Trim().TrimEnd('/');
        }

#if UNITY_EDITOR
        /// <summary>
        /// Clears the play-mode log latch so entering Play Mode logs again after domain reload.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStartupLogLatch()
        {
            startupLogged = false;
            cached = null;
        }
#endif
    }
}
