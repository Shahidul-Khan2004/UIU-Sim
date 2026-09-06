using System;
using System.Text;
using UnityEngine;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Lightweight JWT payload reader (no signature verification — Spring Boot owns that).
    /// Used only so Unity can display identity while the Unity auth flow is tested.
    /// </summary>
    public static class JwtClaimsParser
    {
        /// <summary>
        /// Allowed clock skew in seconds when checking JWT expiry.
        /// Clerk tokens are short-lived (~60 s) so client clock drift can
        /// cause false "JWT expired" errors without a tolerance window.
        /// </summary>
        private const long ClockSkewSeconds = 60;

        [Serializable]
        private class JwtPayload
        {
            public string sub;
            public string email;
            public string username;
            public string preferred_username;
            public long exp;
            public string sid;
        }

        public static bool TryReadClaims(string jwt, out string subject, out string email, out string username, out long exp, out string sid)
        {
            subject = string.Empty;
            email = string.Empty;
            username = string.Empty;
            exp = 0;
            sid = string.Empty;

            if (string.IsNullOrWhiteSpace(jwt))
            {
                return false;
            }

            string[] parts = jwt.Split('.');
            if (parts.Length < 2)
            {
                return false;
            }

            try
            {
                string json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                JwtPayload payload = JsonUtility.FromJson<JwtPayload>(json);
                if (payload == null)
                {
                    return false;
                }

                subject = payload.sub ?? string.Empty;
                email = payload.email ?? string.Empty;
                username = FirstNonEmpty(payload.username, payload.preferred_username, email, subject);
                exp = payload.exp;
                sid = payload.sid ?? string.Empty;

                return !string.IsNullOrWhiteSpace(subject);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JwtClaimsParser] Failed to parse JWT payload: {ex.Message}");
                return false;
            }
        }

        public static bool TryReadExpiration(string jwt, out long exp)
        {
            exp = 0;
            if (TryReadClaims(jwt, out _, out _, out _, out long parsedExp, out _))
            {
                exp = parsedExp;
                return exp > 0;
            }

            return false;
        }

        public static bool TryRead(string jwt, out string subject, out string email, out string username, out bool expired)
        {
            expired = false;
            if (!TryReadClaims(jwt, out subject, out email, out username, out long exp, out _))
            {
                return false;
            }

            if (exp > 0)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                expired = exp + ClockSkewSeconds < now;
            }

            return true;
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

        private static byte[] Base64UrlDecode(string input)
        {
            string padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            return Convert.FromBase64String(padded);
        }
    }
}
