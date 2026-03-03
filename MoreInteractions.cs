namespace MoreInteractions;

public enum InteractionMode
{
    Gameplay,
    HoldInteract,
    ScreenOpen
}

[Flags]
public enum DebugGroup
{
    None = 0,
    State = 1 << 0,
    Input = 1 << 1,
    Raycast = 1 << 2,
    Focus = 1 << 3
}

public interface IWorldScreenInteractable
{
    Transform ScreenTransform { get; }

    bool CanFocus(PlayerController player);
}

public class WorldScreenFocusTarget : MonoBehaviour, IWorldScreenInteractable
{
    public Transform? FocusTransform;

    public Transform ScreenTransform => FocusTransform != null ? FocusTransform : transform;

    public bool CanFocus(PlayerController player)
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy && player != null;
    }

    private void OnEnable()
    {
        WorldScreenRegistry.Register(this);
    }

    private void OnDisable()
    {
        WorldScreenRegistry.Unregister(this);
    }
}

internal interface IInteractionInput
{
    bool IsKeyHeld(KeyCode key);

    bool IsMouseButtonDown(int button);

    float GetScrollDelta();

    Vector3 GetMousePosition();
}

internal sealed class UnityInteractionInput : IInteractionInput
{
    public bool IsKeyHeld(KeyCode key) => Input.GetKey(key);

    public bool IsMouseButtonDown(int button) => Input.GetMouseButtonDown(button);

    public float GetScrollDelta() => Input.mouseScrollDelta.y;

    public Vector3 GetMousePosition() => Input.mousePosition;
}

internal static class WorldScreenRegistry
{
    private static readonly HashSet<IWorldScreenInteractable> Screens = new HashSet<IWorldScreenInteractable>();

    internal static void Register(IWorldScreenInteractable screen)
    {
        if (screen != null)
        {
            Screens.Add(screen);
        }
    }

    internal static void Unregister(IWorldScreenInteractable screen)
    {
        if (screen != null)
        {
            Screens.Remove(screen);
        }
    }

    internal static bool TryFindBestCandidate(PlayerController player, Ray ray, float maxDistance, out IWorldScreenInteractable screen)
    {
        screen = null!;

        if (TryFindFromRaycast(player, ray, maxDistance, out screen))
        {
            return true;
        }

        return TryFindFromRegistered(player, ray.origin, ray.direction, maxDistance, out screen);
    }

    private static bool TryFindFromRaycast(PlayerController player, Ray ray, float maxDistance, out IWorldScreenInteractable screen)
    {
        screen = null!;
        var hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (var i = 0; i < hits.Length; i++)
        {
            var resolved = ResolveFromCollider(player, hits[i].collider);
            if (resolved != null)
            {
                screen = resolved;
                return true;
            }
        }

        return false;
    }

    private static IWorldScreenInteractable? ResolveFromCollider(PlayerController player, Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        var behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
        for (var i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IWorldScreenInteractable screen && screen.CanFocus(player))
            {
                return screen;
            }
        }

        return null;
    }

    private static bool TryFindFromRegistered(PlayerController player, Vector3 origin, Vector3 forward, float maxDistance, out IWorldScreenInteractable screen)
    {
        screen = null!;

        var bestDistance = float.MaxValue;
        foreach (var candidate in Screens)
        {
            if (candidate == null || !candidate.CanFocus(player))
            {
                continue;
            }

            var candidateTransform = candidate.ScreenTransform;
            if (candidateTransform == null)
            {
                continue;
            }

            var toCandidate = candidateTransform.position - origin;
            var distance = toCandidate.magnitude;
            if (distance > maxDistance || distance < 0.001f)
            {
                continue;
            }

            var dir = toCandidate / distance;
            if (Vector3.Dot(forward, dir) < 0.25f)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                screen = candidate;
            }
        }

        return screen != null;
    }
}

internal static class InteractionRaycastService
{
    internal static bool TryInteractFromCursor(PlayerController player, Vector3 mousePosition, float maxDistance, out string detail)
    {
        detail = string.Empty;

        var ui = Singleton<UIManager>.Instance;
        if (ui == null || ui.IsInAnyMenu() || ui.IsInEditTextPopup())
        {
            detail = "UI menu is open.";
            return false;
        }

        if (player == null || player.PlayerCamera == null)
        {
            detail = "Player camera unavailable.";
            return false;
        }

        var ray = player.PlayerCamera.ScreenPointToRay(mousePosition);
        if (!Physics.Raycast(ray, out var hitInfo, maxDistance, player.InteractLayerMask, QueryTriggerInteraction.Ignore))
        {
            detail = "Cursor raycast did not hit an interactable surface.";
            return false;
        }

        player.InteractionWheelUI.ClearInteractionWheel();

        var interactables = new List<IInteractable>();
        interactables.AddRange(hitInfo.collider.GetComponentsInParent<IInteractable>());

        if (interactables.Count == 0)
        {
            detail = $"Hit '{hitInfo.collider.name}' but found no IInteractable.";
            return false;
        }

        if (interactables.Count == 1 && !interactables[0].ShouldUseInteractionWheel())
        {
            var interaction = interactables[0].GetInteractions().FirstOrDefault();
            interactables[0].Interact(interaction);
            detail = $"Interacted directly with '{hitInfo.collider.name}'.";
            return true;
        }

        player.InteractionWheelUI.gameObject.SetActive(value: true);
        for (var i = 0; i < interactables.Count; i++)
        {
            player.InteractionWheelUI.PopulateInteractionWheel(interactables[i]);
        }

        detail = $"Opened interaction wheel for '{hitInfo.collider.name}' ({interactables.Count} options).";
        return true;
    }
}

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class MoreInteractions : BaseUnityPlugin
{
    public const string PluginGuid = "com.moreinteractions";
    public const string PluginName = "MoreInteractions";
    public const string PluginVersion = "0.2.0";

    private static readonly FieldInfo? InteractRangeField = AccessTools.Field(typeof(PlayerController), "_interactRange");

    private static MoreInteractions? s_instance;
    private static ManualLogSource? s_log;

    private readonly IInteractionInput _input = new UnityInteractionInput();

    private Harmony? _harmony;
    private PlayerController? _player;

    private InteractionMode _mode = InteractionMode.Gameplay;

    private float _yawOffset;
    private float _yawVelocity;
    private float _pitchOffset;
    private float _pitchVelocity;

    private float _focusZoomTarget;
    private float _focusZoomCurrent;
    private float _focusZoomVelocity;
    private IWorldScreenInteractable? _focusedScreen;

    private ConfigEntry<bool>? _enabled;
    private ConfigEntry<KeyCode>? _holdKey;
    private ConfigEntry<bool>? _leftClickInteract;
    private ConfigEntry<float>? _cursorInteractMaxDistance;

    private ConfigEntry<float>? _edgeStartNormalized;
    private ConfigEntry<float>? _centerTwistStrength;
    private ConfigEntry<float>? _maxYawDegrees;
    private ConfigEntry<float>? _maxPitchDegrees;
    private ConfigEntry<float>? _smoothingSeconds;

    private ConfigEntry<bool>? _viewModelTwistEnabled;
    private ConfigEntry<float>? _viewModelYawScale;
    private ConfigEntry<float>? _viewModelPitchScale;
    private ConfigEntry<float>? _viewModelMaxYawDegrees;
    private ConfigEntry<float>? _viewModelMaxPitchDegrees;

    private ConfigEntry<bool>? _screenFocusEnabled;
    private ConfigEntry<float>? _screenFocusDistance;
    private ConfigEntry<float>? _screenFocusFov;
    private ConfigEntry<float>? _screenFocusScrollStep;
    private ConfigEntry<float>? _screenFocusZoomSmooth;
    private ConfigEntry<float>? _screenFocusLookSmooth;

    private ConfigEntry<bool>? _debugEnabled;
    private ConfigEntry<bool>? _debugState;
    private ConfigEntry<bool>? _debugInput;
    private ConfigEntry<bool>? _debugRaycast;
    private ConfigEntry<bool>? _debugFocus;
    private ConfigEntry<bool>? _debugOverlay;

    public static event Action<InteractionMode>? ModeChanged;
    public static event Action? HoldModeEntered;
    public static event Action? HoldModeExited;
    public static event Action<IWorldScreenInteractable>? ScreenOpened;
    public static event Action<IWorldScreenInteractable>? ScreenClosed;

    private void Awake()
    {
        s_instance = this;
        s_log = Logger;

        _enabled = Config.Bind("General", "Enabled", true, "Enable MoreInteractions.");
        _holdKey = Config.Bind("General", "HoldKey", KeyCode.F, "Hold this key to enter free-cursor interaction mode.");
        _leftClickInteract = Config.Bind("General", "LeftClickInteract", true, "When active, left click performs an interact raycast from cursor position.");
        _cursorInteractMaxDistance = Config.Bind("General", "CursorInteractMaxDistance", 2.5f, "Maximum distance for cursor interaction raycasts.");

        _edgeStartNormalized = Config.Bind("Camera", "EdgeStartNormalized", 0.6f, "Normalized screen-space threshold (0-1) where extra edge twist acceleration begins.");
        _centerTwistStrength = Config.Bind("Camera", "CenterTwistStrength", 0.2f, "How much gentle camera twist is applied from center toward edge (0 = edge-only, 1 = fully center-weighted).");
        _maxYawDegrees = Config.Bind("Camera", "MaxYawDegrees", 7f, "Maximum yaw twist in degrees while cursor is at screen edge.");
        _maxPitchDegrees = Config.Bind("Camera", "MaxPitchDegrees", 4f, "Maximum pitch twist in degrees while cursor is at screen edge.");
        _smoothingSeconds = Config.Bind("Camera", "SmoothingSeconds", 0.06f, "Smooth time for twist offsets.");

        _viewModelTwistEnabled = Config.Bind("ViewModel", "Enabled", true, "Apply a subtle viewmodel twist while hold mode is active.");
        _viewModelYawScale = Config.Bind("ViewModel", "YawScale", 0.4f, "How much viewmodel yaw follows camera twist.");
        _viewModelPitchScale = Config.Bind("ViewModel", "PitchScale", 0.4f, "How much viewmodel pitch follows camera twist.");
        _viewModelMaxYawDegrees = Config.Bind("ViewModel", "MaxYawDegrees", 3f, "Maximum viewmodel yaw twist.");
        _viewModelMaxPitchDegrees = Config.Bind("ViewModel", "MaxPitchDegrees", 2f, "Maximum viewmodel pitch twist.");

        _screenFocusEnabled = Config.Bind("ScreenFocus", "Enabled", true, "Allow mouse wheel zoom/focus on nearby world screens while hold mode is active.");
        _screenFocusDistance = Config.Bind("ScreenFocus", "MaxDistance", 1.5f, "Maximum distance from camera to a world screen for focus mode.");
        _screenFocusFov = Config.Bind("ScreenFocus", "FocusedFov", 38f, "Target camera FOV while focused on a world screen.");
        _screenFocusScrollStep = Config.Bind("ScreenFocus", "ScrollStep", 0.25f, "Zoom amount added/removed per mouse wheel step.");
        _screenFocusZoomSmooth = Config.Bind("ScreenFocus", "ZoomSmoothSeconds", 0.08f, "Smoothing time for focus zoom transitions.");
        _screenFocusLookSmooth = Config.Bind("ScreenFocus", "LookSmoothSeconds", 0.08f, "Smoothing time for camera look-at while focused.");

        _debugEnabled = Config.Bind("Debug", "Enabled", false, "Enable debug logging and overlay.");
        _debugState = Config.Bind("Debug", "State", true, "Enable state transition debug logs.");
        _debugInput = Config.Bind("Debug", "Input", false, "Enable input debug logs.");
        _debugRaycast = Config.Bind("Debug", "Raycast", false, "Enable interaction raycast debug logs.");
        _debugFocus = Config.Bind("Debug", "Focus", true, "Enable screen focus debug logs.");
        _debugOverlay = Config.Bind("Debug", "Overlay", false, "Show a small runtime overlay with mode/focus info.");

        SanitizeConfigValues();

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(MoreInteractions).Assembly);

        Logger.LogInfo($"{PluginName} {PluginVersion} initialized.");
    }

    private void Update()
    {
        if (_enabled?.Value != true)
        {
            ResetToGameplay();
            return;
        }

        if (!TryResolveGameplayContext(out var player))
        {
            ResetToGameplay();
            return;
        }

        var isHoldPressed = IsHoldModeRequested();
        if (!isHoldPressed)
        {
            ResetToGameplay();
            return;
        }

        EnterHoldMode();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateCameraOffsets();
        UpdateScreenFocus(player);

        if (_leftClickInteract?.Value == true && _input.IsMouseButtonDown(0))
        {
            var mousePosition = _input.GetMousePosition();
            var maxDistance = ResolveCursorInteractDistance(player);
            var interacted = InteractionRaycastService.TryInteractFromCursor(player, mousePosition, maxDistance, out var detail);
            LogDebug(DebugGroup.Raycast, interacted ? $"Click interact success: {detail}" : $"Click interact miss: {detail}");
        }
    }

    private void OnGUI()
    {
        if (_debugEnabled?.Value != true || _debugOverlay?.Value != true)
        {
            return;
        }

        var focusedName = _focusedScreen?.ScreenTransform != null ? _focusedScreen.ScreenTransform.name : "None";
        var text =
            $"Mode: {_mode}\n" +
            $"Focus Zoom: {_focusZoomCurrent:F2}\n" +
            $"Focused Screen: {focusedName}\n" +
            $"Yaw/Pitch Offset: {_yawOffset:F2} / {_pitchOffset:F2}";

        GUI.Box(new Rect(14f, 14f, 310f, 94f), text);
    }

    private void OnDestroy()
    {
        ResetToGameplay();
        _harmony?.UnpatchSelf();
        _harmony = null;

        if (ReferenceEquals(s_instance, this))
        {
            s_instance = null;
        }
    }

    private void SanitizeConfigValues()
    {
        if (_edgeStartNormalized != null)
        {
            _edgeStartNormalized.Value = Mathf.Clamp01(_edgeStartNormalized.Value);
        }

        if (_centerTwistStrength != null)
        {
            _centerTwistStrength.Value = Mathf.Clamp01(_centerTwistStrength.Value);
        }

        if (_maxYawDegrees != null)
        {
            _maxYawDegrees.Value = Mathf.Clamp(_maxYawDegrees.Value, 0f, 45f);
        }

        if (_maxPitchDegrees != null)
        {
            _maxPitchDegrees.Value = Mathf.Clamp(_maxPitchDegrees.Value, 0f, 45f);
        }

        if (_smoothingSeconds != null)
        {
            _smoothingSeconds.Value = Mathf.Clamp(_smoothingSeconds.Value, 0.001f, 0.5f);
        }

        if (_cursorInteractMaxDistance != null)
        {
            _cursorInteractMaxDistance.Value = Mathf.Clamp(_cursorInteractMaxDistance.Value, 0.25f, 10f);
        }

        if (_viewModelYawScale != null)
        {
            _viewModelYawScale.Value = Mathf.Clamp01(_viewModelYawScale.Value);
        }

        if (_viewModelPitchScale != null)
        {
            _viewModelPitchScale.Value = Mathf.Clamp01(_viewModelPitchScale.Value);
        }

        if (_viewModelMaxYawDegrees != null)
        {
            _viewModelMaxYawDegrees.Value = Mathf.Clamp(_viewModelMaxYawDegrees.Value, 0f, 20f);
        }

        if (_viewModelMaxPitchDegrees != null)
        {
            _viewModelMaxPitchDegrees.Value = Mathf.Clamp(_viewModelMaxPitchDegrees.Value, 0f, 20f);
        }

        if (_screenFocusDistance != null)
        {
            _screenFocusDistance.Value = Mathf.Clamp(_screenFocusDistance.Value, 0.25f, 10f);
        }

        if (_screenFocusFov != null)
        {
            _screenFocusFov.Value = Mathf.Clamp(_screenFocusFov.Value, 20f, 80f);
        }

        if (_screenFocusScrollStep != null)
        {
            _screenFocusScrollStep.Value = Mathf.Clamp(_screenFocusScrollStep.Value, 0.01f, 1f);
        }

        if (_screenFocusZoomSmooth != null)
        {
            _screenFocusZoomSmooth.Value = Mathf.Clamp(_screenFocusZoomSmooth.Value, 0.001f, 0.8f);
        }

        if (_screenFocusLookSmooth != null)
        {
            _screenFocusLookSmooth.Value = Mathf.Clamp(_screenFocusLookSmooth.Value, 0.001f, 0.8f);
        }
    }

    private bool TryResolveGameplayContext(out PlayerController player)
    {
        player = null!;

        var ui = Singleton<UIManager>.Instance;
        if (ui == null || ui.IsInAnyMenu() || ui.IsInEditTextPopup())
        {
            return false;
        }

        if (_player == null)
        {
            _player = UnityEngine.Object.FindObjectOfType<PlayerController>();
        }

        player = _player;
        return player != null && player.PlayerCamera != null;
    }

    private float ResolveCursorInteractDistance(PlayerController player)
    {
        var vanillaRange = 2f;
        if (InteractRangeField?.GetValue(player) is float configuredRange)
        {
            vanillaRange = configuredRange;
        }

        var configuredMax = _cursorInteractMaxDistance?.Value ?? 2.5f;
        return Mathf.Max(0.25f, Mathf.Min(vanillaRange, configuredMax));
    }

    private bool IsHoldModeRequested()
    {
        var holdKey = _holdKey?.Value ?? KeyCode.F;
        if (_input.IsKeyHeld(holdKey))
        {
            return true;
        }

        // Match the game's KeybindManager action when using the default interaction hold key.
        if (holdKey != KeyCode.F)
        {
            return false;
        }

        var keybindManager = Singleton<KeybindManager>.Instance;
        if (keybindManager?.Input == null)
        {
            return false;
        }

        return keybindManager.Input.Player.Interact.IsPressed();
    }

    private void EnterHoldMode()
    {
        if (_mode == InteractionMode.Gameplay)
        {
            SetMode(InteractionMode.HoldInteract);
            HoldModeEntered?.Invoke();
        }
    }

    private void ResetToGameplay()
    {
        if (_mode == InteractionMode.Gameplay && _focusZoomCurrent <= 0.0001f)
        {
            return;
        }

        if (_focusedScreen != null)
        {
            var previous = _focusedScreen;
            _focusedScreen = null;
            ScreenClosed?.Invoke(previous);
            LogDebug(DebugGroup.Focus, $"Closed screen '{previous.ScreenTransform.name}' (reset)." );
        }

        if (_mode != InteractionMode.Gameplay)
        {
            HoldModeExited?.Invoke();
            SetMode(InteractionMode.Gameplay);
        }

        _focusZoomTarget = 0f;
        _focusZoomCurrent = 0f;
        _focusZoomVelocity = 0f;

        _yawOffset = 0f;
        _pitchOffset = 0f;
        _yawVelocity = 0f;
        _pitchVelocity = 0f;
    }

    private void UpdateCameraOffsets()
    {
        if (_mode == InteractionMode.ScreenOpen)
        {
            _yawOffset = Mathf.SmoothDamp(_yawOffset, 0f, ref _yawVelocity, Mathf.Max(0.001f, _smoothingSeconds?.Value ?? 0.06f));
            _pitchOffset = Mathf.SmoothDamp(_pitchOffset, 0f, ref _pitchVelocity, Mathf.Max(0.001f, _smoothingSeconds?.Value ?? 0.06f));
            return;
        }

        var width = Mathf.Max(1f, Screen.width);
        var height = Mathf.Max(1f, Screen.height);

        var mouse = _input.GetMousePosition();
        var x = Mathf.Clamp(mouse.x / width * 2f - 1f, -1f, 1f);
        var y = Mathf.Clamp(mouse.y / height * 2f - 1f, -1f, 1f);

        var edgeStart = Mathf.Clamp01(_edgeStartNormalized?.Value ?? 0.6f);
        var centerTwistStrength = Mathf.Clamp01(_centerTwistStrength?.Value ?? 0.2f);
        var targetYaw = ComputeProgressiveOffset(x, edgeStart, _maxYawDegrees?.Value ?? 7f, centerTwistStrength);
        var targetPitch = -ComputeProgressiveOffset(y, edgeStart, _maxPitchDegrees?.Value ?? 4f, centerTwistStrength);
        var smooth = Mathf.Max(0.001f, _smoothingSeconds?.Value ?? 0.06f);

        _yawOffset = Mathf.SmoothDamp(_yawOffset, targetYaw, ref _yawVelocity, smooth);
        _pitchOffset = Mathf.SmoothDamp(_pitchOffset, targetPitch, ref _pitchVelocity, smooth);
    }

    private void UpdateScreenFocus(PlayerController player)
    {
        if (_screenFocusEnabled?.Value != true)
        {
            if (_mode == InteractionMode.ScreenOpen)
            {
                CloseFocusedScreen(keepHoldMode: true);
            }
            return;
        }

        var scroll = _input.GetScrollDelta();
        if (Mathf.Abs(scroll) > 0.001f)
        {
            LogDebug(DebugGroup.Input, $"Scroll delta: {scroll:F3}");
        }

        if (_mode == InteractionMode.ScreenOpen && !IsFocusedScreenStillValid(player))
        {
            LogDebug(DebugGroup.Focus, "Focused screen became invalid or out of range.");
            CloseFocusedScreen(keepHoldMode: true);
        }

        if (scroll > 0f)
        {
            IWorldScreenInteractable? candidate = null;
            if (_focusedScreen == null)
            {
                if (!TryGetScreenCandidate(player, out candidate))
                {
                    LogDebug(DebugGroup.Focus, "Scroll-in requested but no focus candidate was found.");
                    return;
                }
            }

            if (_focusedScreen == null && candidate != null)
            {
                OpenFocusedScreen(candidate);
            }

            var step = Mathf.Max(0.01f, _screenFocusScrollStep?.Value ?? 0.25f);
            _focusZoomTarget = Mathf.Clamp01(_focusZoomTarget + scroll * step);
            if (_focusZoomTarget > 0f && _mode != InteractionMode.ScreenOpen)
            {
                SetMode(InteractionMode.ScreenOpen);
            }

            return;
        }

        if (scroll < 0f && _mode == InteractionMode.ScreenOpen)
        {
            var step = Mathf.Max(0.01f, _screenFocusScrollStep?.Value ?? 0.25f);
            _focusZoomTarget = Mathf.Clamp01(_focusZoomTarget + scroll * step);
            if (_focusZoomTarget <= 0.001f)
            {
                CloseFocusedScreen(keepHoldMode: true);
            }
        }
    }

    private bool TryGetScreenCandidate(PlayerController player, out IWorldScreenInteractable screen)
    {
        screen = null!;

        if (player.PlayerCamera == null)
        {
            return false;
        }

        var maxDistance = Mathf.Max(0.25f, _screenFocusDistance?.Value ?? 1.5f);
        var mousePos = _input.GetMousePosition();
        var ray = player.PlayerCamera.ScreenPointToRay(mousePos);
        if (!WorldScreenRegistry.TryFindBestCandidate(player, ray, maxDistance, out screen))
        {
            return false;
        }

        var distance = Vector3.Distance(player.PlayerCamera.transform.position, screen.ScreenTransform.position);
        if (distance > maxDistance)
        {
            return false;
        }

        return true;
    }

    private bool IsFocusedScreenStillValid(PlayerController player)
    {
        if (_focusedScreen == null || !_focusedScreen.CanFocus(player) || _focusedScreen.ScreenTransform == null)
        {
            return false;
        }

        var maxDistance = Mathf.Max(0.25f, _screenFocusDistance?.Value ?? 1.5f);
        var distance = Vector3.Distance(player.PlayerCamera.transform.position, _focusedScreen.ScreenTransform.position);
        return distance <= maxDistance;
    }

    private void OpenFocusedScreen(IWorldScreenInteractable screen)
    {
        _focusedScreen = screen;
        ScreenOpened?.Invoke(screen);
        LogDebug(DebugGroup.Focus, $"Opened screen '{screen.ScreenTransform.name}'.");
    }

    private void CloseFocusedScreen(bool keepHoldMode)
    {
        if (_focusedScreen != null)
        {
            var previous = _focusedScreen;
            _focusedScreen = null;
            ScreenClosed?.Invoke(previous);
            LogDebug(DebugGroup.Focus, $"Closed screen '{previous.ScreenTransform.name}'.");
        }

        _focusZoomTarget = 0f;
        if (!keepHoldMode)
        {
            SetMode(InteractionMode.Gameplay);
            return;
        }

        if (_mode != InteractionMode.Gameplay)
        {
            SetMode(InteractionMode.HoldInteract);
        }
    }

    private static float ComputeProgressiveOffset(float value, float edgeStart, float maxDegrees, float centerTwistStrength)
    {
        var abs = Mathf.Abs(value);
        var denom = Mathf.Max(0.001f, 1f - edgeStart);
        var edgeT = abs <= edgeStart ? 0f : Mathf.Clamp01((abs - edgeStart) / denom);
        var centerT = Mathf.Pow(Mathf.Clamp01(abs), 1.35f);
        var blendedT = Mathf.Lerp(edgeT, centerT, Mathf.Clamp01(centerTwistStrength));
        return Mathf.Sign(value) * blendedT * Mathf.Max(0f, maxDegrees);
    }

    private void SetMode(InteractionMode next)
    {
        if (_mode == next)
        {
            return;
        }

        var previous = _mode;
        _mode = next;
        ModeChanged?.Invoke(next);
        LogDebug(DebugGroup.State, $"Mode changed: {previous} -> {next}");
    }

    private bool IsDebugGroupEnabled(DebugGroup group)
    {
        if (_debugEnabled?.Value != true || group == DebugGroup.None)
        {
            return false;
        }

        return group switch
        {
            DebugGroup.State => _debugState?.Value == true,
            DebugGroup.Input => _debugInput?.Value == true,
            DebugGroup.Raycast => _debugRaycast?.Value == true,
            DebugGroup.Focus => _debugFocus?.Value == true,
            _ => false
        };
    }

    private void LogDebug(DebugGroup group, string message)
    {
        if (!IsDebugGroupEnabled(group))
        {
            return;
        }

        Logger.LogInfo($"[Debug/{group}] {message}");
    }

    internal static bool IsInteractionModeActive
    {
        get
        {
            var instance = s_instance;
            return instance != null && instance._mode != InteractionMode.Gameplay;
        }
    }

    internal static bool ShouldBlockNativeInteract(PlayerController player)
    {
        var instance = s_instance;
        if (instance == null || instance._enabled?.Value != true)
        {
            return false;
        }

        if (instance._mode != InteractionMode.Gameplay)
        {
            return true;
        }

        if (player == null)
        {
            return false;
        }

        var ui = Singleton<UIManager>.Instance;
        if (ui == null || ui.IsInAnyMenu() || ui.IsInEditTextPopup())
        {
            return false;
        }

        return instance.IsHoldModeRequested();
    }

    internal static void ApplyCameraEffects(PlayerController player)
    {
        var instance = s_instance;
        if (instance == null || player == null || player.PlayerCamera == null)
        {
            return;
        }

        if (instance._mode == InteractionMode.Gameplay)
        {
            return;
        }

        var camera = player.PlayerCamera;

        if (instance._mode != InteractionMode.ScreenOpen)
        {
            var edgeTwist = Quaternion.Euler(instance._pitchOffset, instance._yawOffset, 0f);
            camera.transform.localRotation *= edgeTwist;
        }

        if (instance._mode == InteractionMode.ScreenOpen && instance._focusedScreen != null)
        {
            var zoomSmooth = Mathf.Max(0.001f, instance._screenFocusZoomSmooth?.Value ?? 0.08f);
            instance._focusZoomCurrent = Mathf.SmoothDamp(instance._focusZoomCurrent, instance._focusZoomTarget, ref instance._focusZoomVelocity, zoomSmooth);

            var focusFov = Mathf.Clamp(instance._screenFocusFov?.Value ?? 38f, 20f, 80f);
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, focusFov, instance._focusZoomCurrent);

            var targetTransform = instance._focusedScreen.ScreenTransform;
            if (targetTransform != null)
            {
                var toTarget = targetTransform.position - camera.transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    var desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    var lookSmooth = Mathf.Max(0.001f, instance._screenFocusLookSmooth?.Value ?? 0.08f);
                    var lerpT = 1f - Mathf.Exp(-Time.deltaTime / lookSmooth);
                    camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, desiredRotation, lerpT * instance._focusZoomCurrent);
                }
            }
        }
    }

    internal static void ApplyViewModelTwist(PlayerController player)
    {
        var instance = s_instance;
        if (instance == null || player == null || player.ViewModelContainer == null)
        {
            return;
        }

        if (instance._mode == InteractionMode.Gameplay || instance._viewModelTwistEnabled?.Value != true)
        {
            return;
        }

        var yawScale = Mathf.Clamp(instance._viewModelYawScale?.Value ?? 0.4f, 0f, 1f);
        var pitchScale = Mathf.Clamp(instance._viewModelPitchScale?.Value ?? 0.4f, 0f, 1f);
        var maxYaw = Mathf.Max(0f, instance._viewModelMaxYawDegrees?.Value ?? 3f);
        var maxPitch = Mathf.Max(0f, instance._viewModelMaxPitchDegrees?.Value ?? 2f);

        var yaw = Mathf.Clamp(instance._yawOffset * yawScale, -maxYaw, maxYaw);
        var pitch = Mathf.Clamp(instance._pitchOffset * pitchScale, -maxPitch, maxPitch);
        var twist = Quaternion.Euler(pitch, yaw, 0f);

        player.ViewModelContainer.localRotation *= twist;
    }

    internal static void ForceCursorIfNeeded()
    {
        var instance = s_instance;
        if (instance == null || instance._mode == InteractionMode.Gameplay)
        {
            return;
        }

        var ui = Singleton<UIManager>.Instance;
        if (ui == null || ui.IsInAnyMenu() || ui.IsInEditTextPopup())
        {
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    internal static void LogError(string context, Exception ex)
    {
        (s_log ?? BepInEx.Logging.Logger.CreateLogSource(PluginName)).LogError($"{context}: {ex}");
    }
}

[HarmonyPatch(typeof(PlayerController), "TryInteract")]
internal static class PlayerControllerTryInteractPatch
{
    private static bool Prefix(PlayerController __instance)
    {
        try
        {
            return !MoreInteractions.ShouldBlockNativeInteract(__instance);
        }
        catch (Exception ex)
        {
            MoreInteractions.LogError("TryInteract prefix failed", ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(PlayerController), "HandleCameraBobbing")]
internal static class PlayerControllerHandleCameraBobbingPatch
{
    private static void Postfix(PlayerController __instance)
    {
        try
        {
            MoreInteractions.ApplyCameraEffects(__instance);
        }
        catch (Exception ex)
        {
            MoreInteractions.LogError("HandleCameraBobbing postfix failed", ex);
        }
    }
}

[HarmonyPatch(typeof(PlayerController), "HandleViewModelBobbing")]
internal static class PlayerControllerHandleViewModelBobbingPatch
{
    private static void Postfix(PlayerController __instance)
    {
        try
        {
            MoreInteractions.ApplyViewModelTwist(__instance);
        }
        catch (Exception ex)
        {
            MoreInteractions.LogError("HandleViewModelBobbing postfix failed", ex);
        }
    }
}

[HarmonyPatch(typeof(UIManager), "LateUpdate")]
internal static class UIManagerUpdatePatch
{
    private static void Postfix()
    {
        try
        {
            MoreInteractions.ForceCursorIfNeeded();
        }
        catch (Exception ex)
        {
            MoreInteractions.LogError("UIManager Update postfix failed", ex);
        }
    }
}
