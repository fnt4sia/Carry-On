using UnityEngine;

public class LuggageTimerDisplay : MonoBehaviour
{
    [SerializeField] private Luggage luggage;
    [SerializeField] private BoxCollider sourceCollider;
    [SerializeField] private string worldUiLayerName = WorldUIOverlayCamera.LayerName;
    [SerializeField, Min(0f)] private float hoverHeight = 0.35f;
    [SerializeField, Min(0.05f)] private float displayRadius = 0.6f;
    [SerializeField, Range(12, 96)] private int radialSegments = 48;
    [SerializeField] private Color faceColor = new(0.96f, 0.96f, 0.94f);
    [SerializeField] private Color frameColor = new(0.08f, 0.08f, 0.1f);
    [SerializeField] private Color normalFillColor = new(0.98f, 0.18f, 0.18f);
    [SerializeField] private Color warningFillColor = new(1f, 0.58f, 0.12f);
    [SerializeField] private Color dangerFillColor = new(0.88f, 0.08f, 0.08f);
    [SerializeField] private Color gateNumberColor = new(0.08f, 0.08f, 0.1f);
    [SerializeField] private float warningThreshold = 10f;
    [SerializeField] private float dangerThreshold = 5f;

    private Camera targetCamera;
    private GameObject graphicsRoot;
    private Mesh fillMesh;
    private MeshRenderer fillRenderer;
    private TextMesh gateNumberText;
    private MaterialPropertyBlock propertyBlock;
    private int lastDisplayedSecond = -1;
    private int lastDestinationGateNumber = -1;
    private float lastFillAmount = -1f;

    private static Material sharedUnlitMaterial;

    private void Awake()
    {
        if (luggage == null)
            luggage = GetComponentInParent<Luggage>();

        if (sourceCollider == null && luggage != null)
            sourceCollider = luggage.GetComponent<BoxCollider>();

        DisableLegacyTextRenderer();
        BuildGraphics();
        ApplyWorldUiLayer();
        UpdatePlacementAndFacing();
    }

    private void LateUpdate()
    {
        if (luggage == null || graphicsRoot == null)
            return;

        UpdatePlacementAndFacing();
        UpdateTimer();
    }

    private void DisableLegacyTextRenderer()
    {
        Renderer staleRenderer = GetComponent<Renderer>();
        if (staleRenderer != null)
            staleRenderer.enabled = false;
    }

    private void BuildGraphics()
    {
        graphicsRoot = new GameObject("CircularTimerGraphic");
        graphicsRoot.transform.SetParent(transform, false);

        CreateDisc("Frame", displayRadius, frameColor, 0f);
        CreateDisc("Face", displayRadius * 0.82f, faceColor, 0.002f);

        fillMesh = CreateWedgeMesh(displayRadius * 0.72f, 1f);
        fillRenderer = CreateMeshObject("Fill", fillMesh, normalFillColor, 0.004f);

        CreateDisc("CenterGateNumberBacking", displayRadius * 0.32f, faceColor, 0.006f);
        CreateGateNumberText();
        CreateQuad("TopStem", new Vector2(displayRadius * 0.18f, displayRadius * 0.22f), frameColor, new Vector3(0f, displayRadius * 1.02f, 0.001f));
        CreateQuad("LeftButton", new Vector2(displayRadius * 0.12f, displayRadius * 0.12f), frameColor, new Vector3(-displayRadius * 0.64f, displayRadius * 0.62f, 0.001f));
        CreateQuad("RightButton", new Vector2(displayRadius * 0.12f, displayRadius * 0.12f), frameColor, new Vector3(displayRadius * 0.64f, displayRadius * 0.62f, 0.001f));

        propertyBlock = new MaterialPropertyBlock();
        transform.localScale = Vector3.one;
        graphicsRoot.SetActive(false);
    }

    private void CreateDisc(string name, float radius, Color color, float zOffset)
    {
        Mesh discMesh = CreateDiscMesh(radius);
        CreateMeshObject(name, discMesh, color, zOffset);
    }

    private void CreateQuad(string name, Vector2 size, Color color, Vector3 localPosition)
    {
        Mesh quadMesh = new Mesh
        {
            name = $"{name}Mesh",
            vertices = new[]
            {
                new Vector3(-size.x * 0.5f, -size.y * 0.5f, 0f),
                new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f),
                new Vector3(size.x * 0.5f, size.y * 0.5f, 0f),
                new Vector3(size.x * 0.5f, -size.y * 0.5f, 0f)
            },
            triangles = new[] { 0, 1, 2, 0, 2, 3 }
        };
        quadMesh.RecalculateNormals();
        MeshRenderer renderer = CreateMeshObject(name, quadMesh, color, localPosition.z);
        renderer.transform.localPosition = localPosition;
    }

    private void CreateGateNumberText()
    {
        GameObject textObject = new("GateNumber");
        textObject.transform.SetParent(graphicsRoot.transform, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, 0.01f);

        gateNumberText = textObject.AddComponent<TextMesh>();
        gateNumberText.anchor = TextAnchor.MiddleCenter;
        gateNumberText.alignment = TextAlignment.Center;
        gateNumberText.fontSize = 96;
        gateNumberText.characterSize = displayRadius * 0.55f;
        gateNumberText.color = gateNumberColor;
        gateNumberText.text = string.Empty;
        textObject.SetActive(false);
    }

    private MeshRenderer CreateMeshObject(string name, Mesh mesh, Color color, float zOffset)
    {
        GameObject meshObject = new(name);
        meshObject.transform.SetParent(graphicsRoot.transform, false);
        meshObject.transform.localPosition = new Vector3(0f, 0f, zOffset);

        MeshFilter filter = meshObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetSharedUnlitMaterial();
        SetRendererColor(renderer, color);
        return renderer;
    }

    private void UpdatePlacementAndFacing()
    {
        if (luggage != null)
        {
            Vector3 anchorPosition = sourceCollider != null
                ? sourceCollider.bounds.center
                : luggage.transform.position;
            float topY = sourceCollider != null
                ? sourceCollider.bounds.max.y
                : luggage.transform.position.y;
            transform.position = new Vector3(anchorPosition.x, topY + hoverHeight, anchorPosition.z);
        }

        if (targetCamera == null)
            targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        if (targetCamera == null)
            return;

        Vector3 directionToCamera = targetCamera.transform.position - transform.position;
        if (directionToCamera.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(directionToCamera, targetCamera.transform.up);
    }

    private void UpdateTimer()
    {
        int displaySecond = Mathf.CeilToInt(luggage.LifetimeRemaining);
        bool shouldShow = displaySecond > 0 && !luggage.IsDelivered;
        if (graphicsRoot.activeSelf != shouldShow)
            graphicsRoot.SetActive(shouldShow);

        if (!shouldShow)
            return;

        UpdateGateNumber();

        float fillAmount = luggage.LifetimeNormalized;
        if (!Mathf.Approximately(fillAmount, lastFillAmount))
        {
            lastFillAmount = fillAmount;
            UpdateFillMesh(fillAmount);
        }

        if (displaySecond == lastDisplayedSecond)
            return;

        lastDisplayedSecond = displaySecond;
        Color fillColor = displaySecond <= dangerThreshold
            ? dangerFillColor
            : displaySecond <= warningThreshold
                ? warningFillColor
                : normalFillColor;
        SetRendererColor(fillRenderer, fillColor);
    }

    private void UpdateGateNumber()
    {
        if (gateNumberText == null || luggage == null)
            return;

        int gateNumber = luggage.HasDestinationGate ? luggage.DestinationGateNumber : 0;
        if (gateNumber == lastDestinationGateNumber)
            return;

        lastDestinationGateNumber = gateNumber;
        bool shouldShowGateNumber = gateNumber > 0;
        gateNumberText.gameObject.SetActive(shouldShowGateNumber);

        if (!shouldShowGateNumber)
        {
            gateNumberText.text = string.Empty;
            return;
        }

        string gateText = gateNumber.ToString();
        gateNumberText.text = gateText;
        gateNumberText.characterSize = displayRadius * (gateText.Length > 1 ? 0.42f : 0.55f);
        gateNumberText.color = gateNumberColor;
    }

    public void RefreshImmediate()
    {
        if (graphicsRoot == null)
            return;

        UpdatePlacementAndFacing();
        UpdateTimer();
    }

    private void UpdateFillMesh(float fillAmount)
    {
        if (fillMesh == null)
            return;

        Mesh updatedMesh = CreateWedgeMesh(displayRadius * 0.72f, fillAmount);
        fillMesh.Clear();
        fillMesh.vertices = updatedMesh.vertices;
        fillMesh.triangles = updatedMesh.triangles;
        fillMesh.RecalculateNormals();
    }

    private static Mesh CreateDiscMesh(float radius)
    {
        const int segmentCount = 48;
        Vector3[] vertices = new Vector3[segmentCount + 1];
        int[] triangles = new int[segmentCount * 3];

        vertices[0] = Vector3.zero;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = Mathf.Deg2Rad * (90f - i * (360f / segmentCount));
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == segmentCount - 1 ? 1 : i + 2;
        }

        Mesh mesh = new()
        {
            name = "DiscMesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        return mesh;
    }

    private Mesh CreateWedgeMesh(float radius, float fillAmount)
    {
        fillAmount = Mathf.Clamp01(fillAmount);
        int usedSegments = Mathf.Max(1, Mathf.CeilToInt(radialSegments * fillAmount));
        float totalDegrees = 360f * fillAmount;

        Vector3[] vertices = new Vector3[usedSegments + 2];
        int[] triangles = new int[usedSegments * 3];
        vertices[0] = Vector3.zero;

        for (int i = 0; i <= usedSegments; i++)
        {
            float segmentT = usedSegments == 0 ? 0f : i / (float)usedSegments;
            float angle = Mathf.Deg2Rad * (90f - totalDegrees * segmentT);
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            if (i == usedSegments)
                continue;

            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        Mesh mesh = new()
        {
            name = "TimerFillMesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        return mesh;
    }

    private void ApplyWorldUiLayer()
    {
        int worldUiLayer = LayerMask.NameToLayer(worldUiLayerName);
        if (worldUiLayer < 0)
        {
            Debug.LogWarning($"{nameof(LuggageTimerDisplay)} could not find layer '{worldUiLayerName}'.");
            return;
        }

        SetLayerRecursively(transform, worldUiLayer);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

    private static Material GetSharedUnlitMaterial()
    {
        if (sharedUnlitMaterial != null)
            return sharedUnlitMaterial;

        Shader shader = Shader.Find("Carry On/World UI Always On Top")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Color");

        sharedUnlitMaterial = new Material(shader)
        {
            name = "Runtime World UI Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedUnlitMaterial;
    }

    private void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(propertyBlock);
    }
}
