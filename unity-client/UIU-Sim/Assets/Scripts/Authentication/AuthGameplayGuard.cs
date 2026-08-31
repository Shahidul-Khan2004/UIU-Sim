using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Prevents gameplay if Main is opened without an authenticated session.
    /// Disables sibling gameplay managers before redirect so floors do not load.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class AuthGameplayGuard : MonoBehaviour
    {
        private void Awake()
        {
            AuthHost host = AuthHost.Instance != null ? AuthHost.Instance : AuthHost.EnsureExists();
            UserSession session = host.AuthManager.Session;

            if (session.IsAuthenticated)
            {
                return;
            }

            if (session.TryRestoreAuthenticatedSession())
            {
                return;
            }

            Debug.LogWarning("[AuthGameplayGuard] Unauthenticated access to Main — redirecting to Login.");

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour != this)
                {
                    behaviour.enabled = false;
                }
            }

            SceneManager.LoadScene(AuthSceneNames.Login);
        }
    }
}
