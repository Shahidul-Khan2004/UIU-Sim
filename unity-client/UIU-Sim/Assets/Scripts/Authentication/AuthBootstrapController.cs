using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Entry scene controller: routes to SaveSelection when authenticated, otherwise Login.
    /// </summary>
    public sealed class AuthBootstrapController : MonoBehaviour
    {
        private void Start()
        {
            AuthUiUtility.ShowUiCursor();

            AuthHost host = AuthHost.EnsureExists();
            UserSession session = host.AuthManager.Session;

            if (session.IsAuthenticated || session.TryRestoreAuthenticatedSession())
            {
                host.TokenProvider?.HydrateAccessToken(session.JwtToken);
                Debug.Log("[AuthBootstrap] Authenticated — loading SaveSelection.");
                SceneManager.LoadScene(AuthSceneNames.SaveSelection);
                return;
            }

            Debug.Log("[AuthBootstrap] Not authenticated — loading Login.");
            SceneManager.LoadScene(AuthSceneNames.Login);
        }
    }
}
