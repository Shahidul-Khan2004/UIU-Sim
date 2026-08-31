using UnityEngine;

/// <summary>
/// Marker for a runtime player spawn location. Floor scenes own these; gameplay systems find them after additive load.
/// </summary>
public sealed class PlayerSpawnPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 0.35f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward);
    }
}
