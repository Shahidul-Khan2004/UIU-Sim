using UIU.Simulator.Networking;
using UnityEngine;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Persistent auth host across Bootstrap → Login → Main.
    /// One instance only; created by Bootstrap (or Login fallback), not by gameplay scenes.
    /// </summary>
    public sealed class AuthHost : MonoBehaviour
    {
        public static AuthHost Instance { get; private set; }

        [SerializeField] private string backendBaseUrl = "http://localhost:8080";
        [SerializeField] private bool requireBackendValidation;

        public ClerkAuthManager AuthManager { get; private set; }
        public AuthCallbackHandler CallbackHandler { get; private set; }
        public ApiClient ApiClient { get; private set; }

        public static AuthHost EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            AuthHost existing = FindFirstObjectByType<AuthHost>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject root = new GameObject("AuthHost");
            AuthHost host = root.AddComponent<AuthHost>();
            host.Initialize();
            return host;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            if (ApiClient == null)
            {
                ApiClient = GetComponent<ApiClient>() ?? gameObject.AddComponent<ApiClient>();
            }

            ApiClient.BackendBaseUrl = backendBaseUrl;

            if (CallbackHandler == null)
            {
                CallbackHandler = GetComponent<AuthCallbackHandler>() ?? gameObject.AddComponent<AuthCallbackHandler>();
            }

            if (AuthManager == null)
            {
                AuthManager = GetComponent<ClerkAuthManager>() ?? gameObject.AddComponent<ClerkAuthManager>();
            }

            AuthManager.Configure(backendBaseUrl, ApiClient, CallbackHandler, restoreOnStart: false, requireBackendValidation);
        }
    }
}
