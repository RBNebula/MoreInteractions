using UnityEngine.UI;

namespace MoreInteractions.ScreenUi;

[DisallowMultipleComponent]
[RequireComponent(typeof(WorldScreenUiAnchors))]
internal sealed class FuturisticScreenUiController : MonoBehaviour
{
    private const string CanvasRootName = "WorldSpaceScreenCanvas";

    private bool _initialized;

    private void Start()
    {
        TryInitialize();
    }

    private void OnEnable()
    {
        TryInitialize();
    }

    private void LateUpdate()
    {
        if (_initialized)
        {
            enabled = false;
            return;
        }

        TryInitialize();
    }

    private void TryInitialize()
    {
        if (_initialized)
        {
            return;
        }

        if (!TryGetAnchors(out WorldScreenUiAnchors anchors))
        {
            return;
        }

        Transform? topLeft = anchors.TopLeft;
        Transform? bottomRight = anchors.BottomRight;
        Transform? uiBasis = anchors.UiBasis;
        if (topLeft == null || bottomRight == null)
        {
            Debug.LogWarning($"{ModInfo.LOG_PREFIX} Screen UI markers are missing on '{name}'.");
            return;
        }

        Canvas canvas = GetOrCreateCanvas();
        if (!WorldScreenCanvasFitter.TryFit(canvas, topLeft, bottomRight, uiBasis, out _))
        {
            Debug.LogWarning($"{ModInfo.LOG_PREFIX} Failed to fit world-space screen canvas on '{name}'.");
            return;
        }

        RectTransform? canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            Debug.LogWarning($"{ModInfo.LOG_PREFIX} Screen canvas rect transform was missing on '{name}'.");
            return;
        }

        FuturisticShopScreenUiBuilder.Build(canvasRect);
        _initialized = true;
    }

    private bool TryGetAnchors(out WorldScreenUiAnchors anchors)
    {
        anchors = GetComponent<WorldScreenUiAnchors>()!;
        return anchors != null;
    }

    private Canvas GetOrCreateCanvas()
    {
        Transform? existing = transform.Find(CanvasRootName);
        GameObject canvasObject;
        if (existing != null)
        {
            canvasObject = existing.gameObject;
        }
        else
        {
            canvasObject = new GameObject(CanvasRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();

        scaler.dynamicPixelsPerUnit = 12f;
        scaler.referencePixelsPerUnit = 100f;
        raycaster.enabled = false;

        return canvas;
    }
}
