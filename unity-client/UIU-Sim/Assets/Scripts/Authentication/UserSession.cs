using System;
using UnityEngine;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// In-memory player session with optional PlayerPrefs restore for Editor/MVP.
    /// PlayerPrefs is not OS keychain-level storage.
    /// </summary>
    public sealed class UserSession
    {
        private const string PrefJwtKey = "uiu.auth.jwt";
        private const string PrefUserIdKey = "uiu.auth.userId";
        private const string PrefEmailKey = "uiu.auth.email";
        private const string PrefUsernameKey = "uiu.auth.username";

        public string ClerkUserId { get; private set; } = string.Empty;
        public string JwtToken { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public string Username { get; private set; } = string.Empty;
        public AuthenticationState State { get; private set; } = AuthenticationState.LoggedOut;

        public bool HasToken => !string.IsNullOrWhiteSpace(JwtToken);
        public bool IsAuthenticated => State == AuthenticationState.Authenticated && HasToken;

        public event Action<AuthenticationState> StateChanged;

        public void SetAuthenticating()
        {
            SetState(AuthenticationState.Authenticating);
        }

        public void SetFailed(string reason = null)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                Debug.LogWarning($"[UserSession] Authentication failed: {reason}");
            }

            SetState(AuthenticationState.Failed);
        }

        public void ApplyToken(string jwtToken)
        {
            JwtToken = jwtToken ?? string.Empty;
            Persist();
        }

        public void ApplyPlayerProfile(string clerkUserId, string email, string username)
        {
            ClerkUserId = clerkUserId ?? string.Empty;
            Email = email ?? string.Empty;
            Username = username ?? string.Empty;
            Persist();
            SetState(AuthenticationState.Authenticated);
        }

        public void Clear()
        {
            ClerkUserId = string.Empty;
            JwtToken = string.Empty;
            Email = string.Empty;
            Username = string.Empty;
            PlayerPrefs.DeleteKey(PrefJwtKey);
            PlayerPrefs.DeleteKey(PrefUserIdKey);
            PlayerPrefs.DeleteKey(PrefEmailKey);
            PlayerPrefs.DeleteKey(PrefUsernameKey);
            PlayerPrefs.Save();
            SetState(AuthenticationState.LoggedOut);
        }

        /// <summary>
        /// Restores a previously authenticated local session when the JWT is still present and not expired.
        /// </summary>
        public bool TryRestoreAuthenticatedSession()
        {
            string stored = PlayerPrefs.GetString(PrefJwtKey, string.Empty);
            if (string.IsNullOrWhiteSpace(stored))
            {
                return false;
            }

            if (!JwtClaimsParser.TryRead(stored, out string subject, out string email, out string username, out bool expired)
                || expired)
            {
                Clear();
                return false;
            }

            JwtToken = stored;
            ClerkUserId = FirstNonEmpty(PlayerPrefs.GetString(PrefUserIdKey, string.Empty), subject);
            Email = FirstNonEmpty(PlayerPrefs.GetString(PrefEmailKey, string.Empty), email);
            Username = FirstNonEmpty(PlayerPrefs.GetString(PrefUsernameKey, string.Empty), username);
            SetState(AuthenticationState.Authenticated);
            Debug.Log($"[UserSession] Restored authenticated session for {Username}");
            return true;
        }

        private void Persist()
        {
            if (!HasToken)
            {
                return;
            }

            PlayerPrefs.SetString(PrefJwtKey, JwtToken);
            PlayerPrefs.SetString(PrefUserIdKey, ClerkUserId ?? string.Empty);
            PlayerPrefs.SetString(PrefEmailKey, Email ?? string.Empty);
            PlayerPrefs.SetString(PrefUsernameKey, Username ?? string.Empty);
            PlayerPrefs.Save();
        }

        private void SetState(AuthenticationState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }
}
