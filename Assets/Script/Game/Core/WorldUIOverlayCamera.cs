using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class WorldUIOverlayCamera : MonoBehaviour
{
    public const string LayerName = "WorldUI";

    private const string OverlayCameraName = "World UI Camera";

    [SerializeField] private Camera overlayCamera;

    private Camera baseCamera;
    private UniversalAdditionalCameraData baseCameraData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= EnsureForLoadedScene;
        SceneManager.sceneLoaded += EnsureForLoadedScene;
    }

    private static void EnsureForLoadedScene(Scene scene, LoadSceneMode loadSceneMode)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        WorldUIOverlayCamera overlaySetup = mainCamera.GetComponent<WorldUIOverlayCamera>();
        if (overlaySetup == null)
            overlaySetup = mainCamera.gameObject.AddComponent<WorldUIOverlayCamera>();

        overlaySetup.EnsureOverlayCamera();
    }

    private void Awake()
    {
        EnsureOverlayCamera();
    }

    private void LateUpdate()
    {
        if (overlayCamera == null || baseCamera == null)
            return;

        overlayCamera.fieldOfView = baseCamera.fieldOfView;
        overlayCamera.orthographic = baseCamera.orthographic;
        overlayCamera.orthographicSize = baseCamera.orthographicSize;
        overlayCamera.nearClipPlane = baseCamera.nearClipPlane;
        overlayCamera.farClipPlane = baseCamera.farClipPlane;
    }

    private void EnsureOverlayCamera()
    {
        int worldUiLayer = LayerMask.NameToLayer(LayerName);
        if (worldUiLayer < 0)
        {
            Debug.LogWarning($"{nameof(WorldUIOverlayCamera)} could not find layer '{LayerName}'.");
            return;
        }

        baseCamera = GetComponent<Camera>();
        if (baseCamera == null)
            return;

        baseCameraData = GetComponent<UniversalAdditionalCameraData>();
        if (baseCameraData == null)
            baseCameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();

        int worldUiMask = 1 << worldUiLayer;
        baseCamera.cullingMask &= ~worldUiMask;

        if (overlayCamera == null)
        {
            Transform existingChild = transform.Find(OverlayCameraName);
            overlayCamera = existingChild != null
                ? existingChild.GetComponent<Camera>()
                : null;
        }

        if (overlayCamera == null)
        {
            GameObject overlayObject = new(OverlayCameraName);
            overlayObject.transform.SetParent(transform, false);
            overlayCamera = overlayObject.AddComponent<Camera>();
        }

        overlayCamera.CopyFrom(baseCamera);
        overlayCamera.cullingMask = worldUiMask;
        overlayCamera.clearFlags = CameraClearFlags.Nothing;
        overlayCamera.depth = baseCamera.depth + 1f;
        overlayCamera.useOcclusionCulling = false;

        UniversalAdditionalCameraData overlayCameraData = overlayCamera.GetComponent<UniversalAdditionalCameraData>();
        if (overlayCameraData == null)
            overlayCameraData = overlayCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        overlayCameraData.renderType = CameraRenderType.Overlay;
        overlayCameraData.renderPostProcessing = false;
        overlayCameraData.renderShadows = false;

        if (!baseCameraData.cameraStack.Contains(overlayCamera))
            baseCameraData.cameraStack.Add(overlayCamera);
    }
}
