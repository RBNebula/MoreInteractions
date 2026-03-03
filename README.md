# MoreInteractions

Hold `F` to enter free-cursor interaction mode.
If your in-game `Interact` keybind is remapped, the mod now follows that binding too (when `HoldKey` remains default `F`).

## Core behavior
- Movement stays enabled while holding `F`.
- Vanilla `Interact` trigger is blocked while the interaction modes are active.
- Left click uses a cursor raycast interaction path.
- Edge-based camera twist while in `HoldInteract` mode.
- Camera twist now ramps gently from center and accelerates near edge (`Camera.CenterTwistStrength` + `Camera.EdgeStartNormalized`).
- Optional subtle viewmodel twist to match camera direction.

## Interaction states
- `Gameplay`
- `HoldInteract`
- `ScreenOpen`

## World screen focus
- While holding `F`, scroll in to focus a nearby screen.
- Focus only works when a focus target is within configured range (`1.5m` default).
- Scroll out to return to normal hold view.
- Perspective angle is preserved from your player position.

## Screen target integration
- Implement `IWorldScreenInteractable`, or
- Add `WorldScreenFocusTarget` to a screen object and register automatically.

## Events
- `ModeChanged`
- `HoldModeEntered`
- `HoldModeExited`
- `ScreenOpened`
- `ScreenClosed`

## Debug groups
- `Debug.Enabled`
- `Debug.State`
- `Debug.Input`
- `Debug.Raycast`
- `Debug.Focus`
- `Debug.Overlay`
