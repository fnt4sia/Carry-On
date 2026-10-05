using UnityEngine;

/// <summary>
/// Slides every child cloud along the wind and wraps it back to the upwind edge of the area.
/// The map's clouds are shadow-only casters — never seen, only their shadows crossing the sea
/// and the islands — so the wrap can happen at an edge the camera never reaches.
///
/// Not MainMenu's CloudDrift: that one frames a fixed camera's sky, spawning clouds out of view
/// and destroying them once they're downwind and unseen, which would delete shadow-only clouds
/// on their first frame.
/// </summary>
public class CloudShadowDrift : MonoBehaviour
{
    [Tooltip("Metres per second across the map, on the XZ plane.")]
    [SerializeField] private Vector2 wind = new(2.5f, -1.2f);
    [Tooltip("World-space XZ rectangle the clouds wrap inside. Keep it wider than anything " +
             "the camera shows, so the wrap is never on screen.")]
    [SerializeField] private Rect area = new(-202f, -287f, 400f, 400f);

    private void Update()
    {
        Vector3 step = new Vector3(wind.x, 0f, wind.y) * Time.deltaTime;
        foreach (Transform cloud in transform)
        {
            Vector3 p = cloud.position + step;
            p.x = area.xMin + Mathf.Repeat(p.x - area.xMin, area.width);
            p.z = area.yMin + Mathf.Repeat(p.z - area.yMin, area.height);
            cloud.position = p;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawWireCube(new Vector3(area.center.x, transform.position.y, area.center.y),
            new Vector3(area.width, 0.1f, area.height));
    }
}
