using System;
using UnityEngine;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Receives authentication callbacks (deep link / absolute URL) and forwards the JWT.
    /// Includes an Editor-friendly manual token paste path for debugging when deep links are unavailable.
    /// </summary>
    public sealed class AuthCallbackHandler : MonoBehaviour
    {
        private ClerkAuthManager authManager;

        public void Initialize(ClerkAuthManager manager)
        {
            authManager = manager;
        }

        private void OnEnable()
        {
            Application.deepLinkActivated += OnDeepLinkActivated;
            TryHandleAbsoluteUrl(Application.absoluteURL);
        }

        private void OnDisable()
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
        }

        private void OnDeepLinkActivated(string url)
        {
            TryHandleAbsoluteUrl(url);
        }

        public void TryHandleAbsoluteUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (!TryExtractToken(url, out string token))
            {
                if (IsKnownAuthScheme(url))
                {
                    Debug.LogWarning($"[AuthCallbackHandler] Deep link missing token: {SanitizeUrl(url)}");
                }

                return;
            }

            Debug.Log("[AuthCallbackHandler] Received auth callback token");
            if (authManager == null)
            {
                Debug.LogError("[AuthCallbackHandler] ClerkAuthManager not initialized");
                return;
            }

            authManager.HandleAuthCallbackToken(token);
        }

        /// <summary>
        /// Editor / debug helper: paste a raw JWT when the custom URL scheme cannot wake the Editor.
        /// </summary>
        public void ApplyManualToken(string jwtToken)
        {
            if (authManager == null)
            {
                Debug.LogError("[AuthCallbackHandler] ClerkAuthManager not initialized");
                return;
            }

            authManager.HandleAuthCallbackToken(jwtToken);
        }

        public static bool TryExtractToken(string url, out string token)
        {
            token = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            // Expected: uiusim://auth/callback?token=... (or uiu-simulator://...)
            // Note: custom schemes are NOT registered for Unity Editor on Linux.
            int queryIndex = url.IndexOf('?');
            if (queryIndex < 0 || queryIndex >= url.Length - 1)
            {
                return false;
            }

            string query = url.Substring(queryIndex + 1);
            string[] parts = query.Split('&');
            foreach (string part in parts)
            {
                string[] kv = part.Split(new[] { '=' }, 2);
                if (kv.Length == 2 && string.Equals(kv[0], "token", StringComparison.OrdinalIgnoreCase))
                {
                    token = Uri.UnescapeDataString(kv[1]);
                    return !string.IsNullOrWhiteSpace(token);
                }
            }

            return false;
        }

        private static bool IsKnownAuthScheme(string url)
        {
            return url.StartsWith("uiusim://", StringComparison.OrdinalIgnoreCase)
                   || url.StartsWith("uiu-simulator://", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeUrl(string url)
        {
            int tokenIndex = url.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
            if (tokenIndex < 0)
            {
                return url;
            }

            return url.Substring(0, tokenIndex + 6) + "***";
        }
    }
}
