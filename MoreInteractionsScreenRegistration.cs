using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using ModMogul;
using MoreInteractions.ScreenUi;

namespace MoreInteractions;

public sealed class WorldScreenUiAnchors : MonoBehaviour
{
    public Transform? TopLeft;

    public Transform? BottomRight;

    public Transform? UiBasis;

    public Transform? ScreenCenter;
}

internal sealed class MoreInteractionsRuntime : IDisposable
{
    private readonly MoreInteractionsScreenRegistrationService _screenRegistrationService;

    internal MoreInteractionsRuntime(ManualLogSource logger)
    {
        _screenRegistrationService = new MoreInteractionsScreenRegistrationService(logger);
    }

    internal Task InitializeAsync()
    {
        return _screenRegistrationService.InitializeAsync();
    }

    public void Dispose()
    {
        _screenRegistrationService.Dispose();
    }
}

internal sealed class MoreInteractionsScreenRegistrationService : IDisposable
{
    private static readonly MethodInfo? RegisterItemWithIconMethod = typeof(Itemizer)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .FirstOrDefault(method =>
        {
            if (!string.Equals(method.Name, "RegisterItem", StringComparison.Ordinal))
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 2 &&
                   parameters[0].ParameterType == typeof(Itemizer.ItemSpec) &&
                   parameters[1].ParameterType == typeof(string);
        });

    private static readonly MethodInfo? RegisterItemMethod = typeof(Itemizer)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .FirstOrDefault(method =>
        {
            if (!string.Equals(method.Name, "RegisterItem", StringComparison.Ordinal))
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 1 &&
                   parameters[0].ParameterType == typeof(Itemizer.ItemSpec);
        });

    private readonly ManualLogSource _logger;
    private readonly CancellationTokenSource _cancellation = new();
    private bool _isInitialized;

    internal MoreInteractionsScreenRegistrationService(ManualLogSource logger)
    {
        _logger = logger;
    }

    internal async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        MoreInteractionsScreenPlan plan = MoreInteractionsScreenPlan.Default;
        BuildingObject prefab = RegisterScreen(plan);
        ConfigurePrefab(prefab, plan);
        await LoadModelAsync(prefab, plan, _cancellation.Token);

        _isInitialized = true;
    }

    public void Dispose()
    {
        if (!_cancellation.IsCancellationRequested)
        {
            _cancellation.Cancel();
        }

        _cancellation.Dispose();
    }

    private BuildingObject RegisterScreen(MoreInteractionsScreenPlan plan)
    {
        Itemizer.ItemSpec spec = new(
            blockID: plan.BlockId,
            internalName: plan.InternalName,
            displayName: plan.DisplayName,
            description: plan.Description,
            price: plan.Price,
            shopCategory: plan.ShopCategory,
            maxStackSize: plan.MaxStackSize,
            isLockedByDefault: false,
            qFunction: "Rotate");

        object? result = RegisterItemMethod?.Invoke(null, new object[] { spec });
        if (result is BuildingObject prefab)
        {
            return prefab;
        }

        result = RegisterItemWithIconMethod?.Invoke(null, new object[] { spec, string.Empty });
        if (result is BuildingObject iconPrefab)
        {
            return iconPrefab;
        }

        throw new InvalidOperationException("Unable to register Futuristic Screen with ModMogul Itemizer.");
    }

    private void ConfigurePrefab(BuildingObject prefab, MoreInteractionsScreenPlan plan)
    {
        RemoveDefaultVisuals(prefab.gameObject);

        Transform visualRoot = GetOrCreateChild(prefab.transform, "VisualRoot");
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one * plan.VisualScale;

        Transform focusAnchor = GetOrCreateChild(prefab.transform, "ScreenFocusAnchor");
        focusAnchor.localPosition = new Vector3(0f, 0.75f, 0f);
        focusAnchor.localRotation = Quaternion.identity;
        focusAnchor.localScale = Vector3.one;

        WorldScreenFocusTarget focusTarget = prefab.GetComponent<WorldScreenFocusTarget>() ?? prefab.gameObject.AddComponent<WorldScreenFocusTarget>();
        focusTarget.FocusTransform = focusAnchor;
        _ = prefab.GetComponent<FuturisticScreenUiController>() ?? prefab.gameObject.AddComponent<FuturisticScreenUiController>();

        prefab.RequiresFlatGround = false;
        prefab.PlacementNodeRequirement = PlacementNodeRequirement.None;
        prefab.SupportType = SupportType.None;
        prefab.BuildingSupportsEnabled = false;
        prefab.RotatingShouldMirrorWhenSnapped = false;
        prefab.BuildModePlacementOffset = Vector3.zero;
        prefab.PhysicalColliderObject = prefab.gameObject;

        if (prefab.Definition != null)
        {
            prefab.Definition.FallbackName = plan.DisplayName;
            prefab.Definition.FallbackDescription = plan.Description;
            prefab.Definition.CanBePlacedInTerrain = true;
            prefab.Definition.UseReverseRotationDirection = false;
#pragma warning disable CS0612
            prefab.Definition.QButtonFunction = "Rotate";
#pragma warning restore CS0612
        }

        ConfigurePlacementCollider(prefab.BuildingPlacementColliderObject, plan.DefaultFootprint);
        ConfigurePhysicalCollider(prefab.gameObject, plan.DefaultBounds);
    }

    private async Task LoadModelAsync(BuildingObject prefab, MoreInteractionsScreenPlan plan, CancellationToken cancellationToken)
    {
        string modelPath = GetModelPath(plan.ModelFileName);
        if (!File.Exists(modelPath))
        {
            _logger.LogWarning($"{ModInfo.LOG_PREFIX} Model file not found: {modelPath}");
            return;
        }

        Transform visualRoot = GetOrCreateChild(prefab.transform, "VisualRoot");
        ClearChildren(visualRoot);

        GameObject? loadedVisual = await ModelMaker.LoadModelToGameObjectAsync(
            modelPath,
            visualRoot,
            ModelMaker.MaterialType.Opaque,
            childName: plan.InternalName + "_Visual",
            ct: cancellationToken);

        if (loadedVisual == null)
        {
            _logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to load model for '{plan.DisplayName}'.");
            return;
        }

        visualRoot.localScale = Vector3.one * plan.VisualScale;

        Bounds bounds = plan.DefaultBounds;
        if (TryNormalizeVisualToGroundPlane(loadedVisual.transform, visualRoot, out Bounds normalizedBounds))
        {
            bounds = normalizedBounds;
        }

        ConfigurePlacementCollider(prefab.BuildingPlacementColliderObject, CreateFootprintFromBounds(bounds));
        ConfigureUiAnchors(prefab.transform, visualRoot, bounds);
        ReplaceRuntimeColliderWithModelColliders(prefab, visualRoot.gameObject);
    }

    private static void RemoveDefaultVisuals(GameObject root)
    {
        MeshFilter? meshFilter = root.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            UnityEngine.Object.Destroy(meshFilter);
        }

        MeshRenderer? meshRenderer = root.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            UnityEngine.Object.Destroy(meshRenderer);
        }
    }

    private static void ConfigurePlacementCollider(GameObject placementObject, MoreInteractionsFootprint footprint)
    {
        BoxCollider collider = placementObject.GetComponent<BoxCollider>() ?? placementObject.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.center = footprint.Center;
        collider.size = footprint.Size;
    }

    private static void ConfigurePhysicalCollider(GameObject root, Bounds bounds)
    {
        BoxCollider collider = root.GetComponent<BoxCollider>() ?? root.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.center = bounds.center;
        collider.size = new Vector3(
            Mathf.Max(0.05f, bounds.size.x),
            Mathf.Max(0.05f, bounds.size.y),
            Mathf.Max(0.05f, bounds.size.z));
    }

    private static bool TryNormalizeVisualToGroundPlane(Transform loadedVisual, Transform visualRoot, out Bounds normalizedBounds)
    {
        normalizedBounds = default;
        if (!TryGetRendererBoundsInLocalSpace(visualRoot, out Bounds currentBounds))
        {
            return false;
        }

        Vector3 offset = new(-currentBounds.center.x, -currentBounds.min.y, -currentBounds.center.z);
        loadedVisual.localPosition += offset;

        if (!TryGetRendererBoundsInLocalSpace(visualRoot, out normalizedBounds))
        {
            normalizedBounds = currentBounds;
        }

        return true;
    }

    private static void ConfigureUiAnchors(Transform prefabRoot, Transform visualRoot, Bounds bounds)
    {
        WorldScreenUiAnchors uiAnchors = prefabRoot.GetComponent<WorldScreenUiAnchors>() ?? prefabRoot.gameObject.AddComponent<WorldScreenUiAnchors>();
        Transform focusAnchor = GetOrCreateChild(prefabRoot, "ScreenFocusAnchor");
        uiAnchors.ScreenCenter = focusAnchor;
        uiAnchors.UiBasis = TryFindNamedChild(visualRoot, "Screen_UI_Basis", out Transform? uiBasis) ? uiBasis : null;

        if (TryFindNamedChild(visualRoot, "TL_Corner", out Transform? topLeft) &&
            TryFindNamedChild(visualRoot, "BR_Corner", out Transform? bottomRight))
        {
            Transform resolvedTopLeft = topLeft!;
            Transform resolvedBottomRight = bottomRight!;
            uiAnchors.TopLeft = resolvedTopLeft;
            uiAnchors.BottomRight = resolvedBottomRight;
            focusAnchor.position = Vector3.Lerp(resolvedTopLeft.position, resolvedBottomRight.position, 0.5f);
            focusAnchor.rotation = uiAnchors.UiBasis != null ? uiAnchors.UiBasis.rotation : resolvedTopLeft.rotation;
            return;
        }

        uiAnchors.TopLeft = null;
        uiAnchors.BottomRight = null;
        focusAnchor.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
        focusAnchor.rotation = uiAnchors.UiBasis != null ? uiAnchors.UiBasis.rotation : Quaternion.identity;
    }

    private static MoreInteractionsFootprint CreateFootprintFromBounds(Bounds bounds)
    {
        float width = Mathf.Max(0.1f, bounds.size.x + 0.02f);
        float length = Mathf.Max(0.1f, bounds.size.z + 0.02f);
        return new MoreInteractionsFootprint(
            new Vector3(bounds.center.x, 0.05f, bounds.center.z),
            new Vector3(width, 0.1f, length));
    }

    private static void ReplaceRuntimeColliderWithModelColliders(BuildingObject prefab, GameObject visualRoot)
    {
        BoxCollider? rootCollider = prefab.gameObject.GetComponent<BoxCollider>();
        if (rootCollider != null)
        {
            UnityEngine.Object.Destroy(rootCollider);
        }

        foreach (MeshCollider existingCollider in visualRoot.GetComponentsInChildren<MeshCollider>(includeInactive: true))
        {
            UnityEngine.Object.Destroy(existingCollider);
        }

        foreach (MeshFilter meshFilter in visualRoot.GetComponentsInChildren<MeshFilter>(includeInactive: true))
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            GameObject meshObject = meshFilter.gameObject;
            MeshCollider meshCollider = meshObject.GetComponent<MeshCollider>() ?? meshObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            meshCollider.isTrigger = false;
        }
    }

    private static bool TryGetRendererBoundsInLocalSpace(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bool hasBounds = false;
        Vector3 min = default;
        Vector3 max = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3[] corners =
            {
                new(rendererBounds.min.x, rendererBounds.min.y, rendererBounds.min.z),
                new(rendererBounds.min.x, rendererBounds.min.y, rendererBounds.max.z),
                new(rendererBounds.min.x, rendererBounds.max.y, rendererBounds.min.z),
                new(rendererBounds.min.x, rendererBounds.max.y, rendererBounds.max.z),
                new(rendererBounds.max.x, rendererBounds.min.y, rendererBounds.min.z),
                new(rendererBounds.max.x, rendererBounds.min.y, rendererBounds.max.z),
                new(rendererBounds.max.x, rendererBounds.max.y, rendererBounds.min.z),
                new(rendererBounds.max.x, rendererBounds.max.y, rendererBounds.max.z)
            };

            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                Vector3 localCorner = root.InverseTransformPoint(corners[cornerIndex]);
                if (!hasBounds)
                {
                    min = localCorner;
                    max = localCorner;
                    hasBounds = true;
                    continue;
                }

                min = Vector3.Min(min, localCorner);
                max = Vector3.Max(max, localCorner);
            }
        }

        if (!hasBounds)
        {
            bounds = default;
            return false;
        }

        bounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform? child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static bool TryFindNamedChild(Transform root, string childName, out Transform? child)
    {
        if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
        {
            child = root;
            return true;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform nestedChild = root.GetChild(i);
            if (TryFindNamedChild(nestedChild, childName, out child))
            {
                return true;
            }
        }

        child = null;
        return false;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.Destroy(parent.GetChild(index).gameObject);
        }
    }

    private static string GetModelPath(string modelFileName)
    {
        string assemblyDirectory = Path.GetDirectoryName(typeof(MoreInteractionsScreenRegistrationService).Assembly.Location)
            ?? string.Empty;
        return Path.Combine(assemblyDirectory, "Models", modelFileName);
    }
}

internal struct MoreInteractionsFootprint
{
    internal MoreInteractionsFootprint(Vector3 center, Vector3 size)
    {
        Center = center;
        Size = size;
    }

    internal Vector3 Center { get; }

    internal Vector3 Size { get; }
}

internal struct MoreInteractionsScreenPlan
{
    internal int BlockId { get; }

    internal string InternalName { get; }

    internal string DisplayName { get; }

    internal string Description { get; }

    internal string ShopCategory { get; }

    internal int Price { get; }

    internal int MaxStackSize { get; }

    internal string ModelFileName { get; }

    internal float VisualScale { get; }

    internal Bounds DefaultBounds { get; }

    internal MoreInteractionsFootprint DefaultFootprint { get; }

    internal MoreInteractionsScreenPlan(
        int blockId,
        string internalName,
        string displayName,
        string description,
        string shopCategory,
        int price,
        int maxStackSize,
        string modelFileName,
        float visualScale,
        Bounds defaultBounds,
        MoreInteractionsFootprint defaultFootprint)
    {
        BlockId = blockId;
        InternalName = internalName;
        DisplayName = displayName;
        Description = description;
        ShopCategory = shopCategory;
        Price = price;
        MaxStackSize = maxStackSize;
        ModelFileName = modelFileName;
        VisualScale = visualScale;
        DefaultBounds = defaultBounds;
        DefaultFootprint = defaultFootprint;
    }

    internal static MoreInteractionsScreenPlan Default => new MoreInteractionsScreenPlan(
        blockId: 46021,
        internalName: "moreinteractions_futuristic_screen",
        displayName: "Futuristic Screen",
        description: "A placeable futuristic display for world-screen focus testing and interaction.",
        shopCategory: "Decoration",
        price: 350,
        maxStackSize: 20,
        modelFileName: "FuturisticScreen2.glb",
        visualScale: 1f,
        defaultBounds: new Bounds(new Vector3(0f, 0.75f, 0f), new Vector3(0.8f, 1.5f, 0.3f)),
        defaultFootprint: new MoreInteractionsFootprint(new Vector3(0f, 0.05f, 0f), new Vector3(0.8f, 0.1f, 0.3f)));
}
