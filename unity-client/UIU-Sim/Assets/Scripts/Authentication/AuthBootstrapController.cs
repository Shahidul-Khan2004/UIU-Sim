using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIU.Simulator.Authentication
{
    /// <summary>
    /// Entry scene controller: routes to Main when authenticated, otherwise Login.
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
                Debug.Log("[AuthBootstrap] Authenticated — loading Main.");
                SceneManager.LoadScene(AuthSceneNames.Main);
                return;
            }

            Debug.Log("[AuthBootstrap] Not authenticated — loading Login.");
            SceneManager.LoadScene(AuthSceneNames.Login);
        }
    }
}
