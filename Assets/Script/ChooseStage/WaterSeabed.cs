using UnityEngine;

/// <summary>
/// Hands the stylized water shader the seabed — the terrain's heightmap plus every island's
/// shallow shelf (<see cref="IslandShallows"/>) — so the sea can colour itself by real depth:
/// turquoise shallows, deep blue, a foam rim at the shore. The heightmap is the terrain's live
/// GPU copy and the shelves are re-read every frame, so sculpting the terrain or moving an
/// island updates the water with no bake step.
///
/// The water can't read the camera depth texture for this: it writes that texture itself, which
/// is what stops the outline pass drawing whatever lies under the sea (see StylizedWater.shader).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class WaterSeabed : MonoBehaviour
{
    [SerializeField] private Terrain seabed;

    private static readonly int HeightmapId = Shader.PropertyToID("_SeabedHeightmap");
    private static readonly int OriginId = Shader.PropertyToID("_SeabedOrigin");
    private static readonly int SizeId = Shader.PropertyToID("_SeabedSize");
    private static readonly int ShallowsId = Shader.PropertyToID("_IslandShallows");
    private static readonly int ShallowCountId = Shader.PropertyToID("_IslandShallowCount");

    // Must match MAX_ISLAND_SHALLOWS in StylizedWater.shader.
    private const int MaxIslands = 32;

    // TerrainData stores heights in half of the R16 range; a stored value of 32766/65535 is
    // the full terrain height.
    private const float MaxStoredHeight = 32766f / 65535f;

    private Renderer waterRenderer;
    private MaterialPropertyBlock block;
    private readonly Vector4[] shelves = new Vector4[MaxIslands];
    private bool warnedTooMany;

    private void OnEnable()
    {
        waterRenderer = GetComponent<Renderer>();
        block ??= new MaterialPropertyBlock();
        Bind();
    }

    // The heightmap texture is recreated when the terrain is edited or reloaded, so rebind
    // every frame rather than trusting the reference from OnEnable.
    private void LateUpdate() => Bind();

    private void OnValidate()
    {
        if (isActiveAndEnabled)
            OnEnable();
    }

    private void Bind()
    {
        if (waterRenderer == null)
            return;

        waterRenderer.GetPropertyBlock(block);
        TerrainData data = seabed != null ? seabed.terrainData : null;
        if (data == null || data.heightmapTexture == null)
        {
            block.SetVector(OriginId, Vector4.zero);
        }
        else
        {
            Vector3 corner = seabed.transform.position;
            Vector3 size = data.size;
            block.SetTexture(HeightmapId, data.heightmapTexture);
            block.SetVector(OriginId, new Vector4(corner.x, corner.y, corner.z, 1f));
            block.SetVector(SizeId, new Vector4(size.x, size.y / MaxStoredHeight, size.z, data.heightmapResolution));
        }

        int count = 0;
        foreach (IslandShallows island in IslandShallows.Active)
            if (island != null && count < MaxIslands)
                shelves[count++] = island.Shelf;
        if (IslandShallows.Active.Count > MaxIslands && !warnedTooMany)
        {
            warnedTooMany = true;
            Debug.LogWarning($"The water draws shallows for {MaxIslands} islands at most; " +
                             $"{IslandShallows.Active.Count - MaxIslands} have none.", this);
        }
        block.SetVectorArray(ShallowsId, shelves);
        block.SetFloat(ShallowCountId, count);

        waterRenderer.SetPropertyBlock(block);
    }
}
