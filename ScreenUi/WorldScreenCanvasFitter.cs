using UnityEngine.UI;

namespace MoreInteractions.ScreenUi;

internal readonly struct WorldScreenCanvasFitResult
{
    internal WorldScreenCanvasFitResult(Vector2 size, Vector3 center, Quaternion rotation)
    {
        Size = size;
        Center = center;
        Rotation = rotation;
    }

    internal Vector2 Size { get; }

    internal Vector3 Center { get; }

    internal Quaternion Rotation { get; }
}

internal static class WorldScreenCanvasFitter
{
    internal static bool TryFit(Canvas canvas, Transform topLeft, Transform bottomRight, Transform? uiBasis, out WorldScreenCanvasFitResult fitResult)
    {
        fitResult = default;
        if (canvas == null || topLeft == null || bottomRight == null)
        {
            return false;
        }

        if (!TryBuildBasis(topLeft, bottomRight, uiBasis, out Vector3 right, out Vector3 up, out Vector3 normal, out float width, out float height))
        {
            return false;
        }

        Transform canvasTransform = canvas.transform;
        RectTransform rectTransform = canvas.GetComponent<RectTransform>();
        Vector3 center = topLeft.position + (right * (width * 0.5f)) - (up * (height * 0.5f));
        Quaternion rotation = Quaternion.LookRotation(normal, up);

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;
        canvas.planeDistance = 0.01f;
        canvas.sortingOrder = 64;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

        canvasTransform.position = center;
        canvasTransform.rotation = rotation;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = FuturisticShopScreenUiBuilder.ReferenceCanvasSize;
        rectTransform.localScale = new Vector3(
            width / FuturisticShopScreenUiBuilder.ReferenceCanvasSize.x,
            height / FuturisticShopScreenUiBuilder.ReferenceCanvasSize.y,
            1f);

        fitResult = new WorldScreenCanvasFitResult(new Vector2(width, height), center, rotation);
        return true;
    }

    private static bool TryBuildBasis(
        Transform topLeft,
        Transform bottomRight,
        Transform? uiBasis,
        out Vector3 right,
        out Vector3 up,
        out Vector3 normal,
        out float width,
        out float height)
    {
        right = (uiBasis ?? topLeft).right.normalized;
        up = Vector3.zero;
        normal = Vector3.zero;
        width = 0f;
        height = 0f;

        if (right.sqrMagnitude < 0.9f)
        {
            return false;
        }

        Vector3 delta = bottomRight.position - topLeft.position;
        if (delta.sqrMagnitude < 0.000001f)
        {
            return false;
        }

        if (Vector3.Dot(delta, right) < 0f)
        {
            right = -right;
        }

        width = Mathf.Abs(Vector3.Dot(delta, right));
        Vector3 verticalComponent = delta - (right * Vector3.Dot(delta, right));
        if (verticalComponent.sqrMagnitude < 0.000001f)
        {
            return false;
        }

        up = (-verticalComponent).normalized;
        height = verticalComponent.magnitude;
        normal = Vector3.Cross(right, up).normalized;
        if (up.sqrMagnitude < 0.9f || normal.sqrMagnitude < 0.9f)
        {
            return false;
        }

        return width > 0.0001f && height > 0.0001f;
    }
}
