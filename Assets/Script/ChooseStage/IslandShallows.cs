using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shallow shelf around one island on the stage-select map: a flat platform just under the
/// surface that slopes down to the sea floor. Nothing is sculpted into the terrain — the water
/// shader draws the shelf (turquoise shallows, the foam rim, the shore waves) from every island's
/// position, so an island carries its shallows wherever it's moved, and the shelves of islands
/// close together merge into one. <see cref="WaterSeabed"/> hands them to the water.
///
/// Sits on the island prefabs (Island_airport, Island_LightHouse, Island_Rock), so every copy of
/// an island has one.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class IslandShallows : MonoBehaviour
{
    [Tooltip("Centre of the shelf from this island's pivot, in its own space: the middle of the " +
             "island's footprint.")]
    [SerializeField] private Vector3 centerOffset;
    [Tooltip("Radius of the flat platform just under the surface, in metres. Its water reads as foam.")]
    [SerializeField, Min(0f)] private float platformRadius = 8f;
    [Tooltip("How far the slope runs from the platform's edge down to the sea floor, in metres.")]
    [SerializeField, Min(0.1f)] private float slopeWidth = 6f;

    private static readonly List<IslandShallows> active = new();

    /// <summary>Every enabled island.</summary>
    public static IReadOnlyList<IslandShallows> Active => active;

    /// <summary>xy = shelf centre (world x, z), z = platform radius, w = slope width, in metres.</summary>
    public Vector4 Shelf
    {
        get
        {
            Vector3 centre = transform.TransformPoint(centerOffset);
            float scale = transform.lossyScale.x;
            return new Vector4(centre.x, centre.z, platformRadius * scale, slopeWidth * scale);
        }
    }

    private void OnEnable() => active.Add(this);

    private void OnDisable() => active.Remove(this);

    private void OnDrawGizmosSelected()
    {
        // Flattened spheres: the platform edge and where the slope reaches the sea floor.
        Vector4 shelf = Shelf;
        Gizmos.color = new Color(0.3f, 0.9f, 0.9f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(new Vector3(shelf.x, transform.position.y, shelf.y),
            Quaternion.identity, new Vector3(1f, 0.001f, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, shelf.z);
        Gizmos.DrawWireSphere(Vector3.zero, shelf.z + shelf.w);
    }
}
