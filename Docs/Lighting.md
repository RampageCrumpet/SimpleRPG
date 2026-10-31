# Lighting

Dungeon rooms are generated at runtime, so nothing can be baked: every light is a realtime URP additional light. The Medium preset allows one additional light per object and the Low presets four, so lights must stay few, short-ranged and shadowless unless a room needs a shadow.

## Torches

`Assets/Prefabs/World/LevelComponents/TestDungeon/Torch_Lit.prefab` is a variant of the pack's `Torch_01 Variant` with a child `Flame Light` (point light, warm orange, range 9 m, no shadows) carrying the `Flicker` script.

`Flicker` (`Assets/Scripts/World/Flicker.cs`) is the generic flame primitive; braziers, candles and lanterns reuse it with different numbers.

- Intensity, range, colour and position drift on layered Perlin noise: a slow sway plus a fast crackle (`crackleAmount` mixes them). The flame value is then eased toward the noise over `smoothing` seconds (frame-rate independent), so it breathes instead of popping. Defaults are deliberately calm: a 0.85 to 1.08 intensity band, sway 0.7 Hz and crackle 3.5 Hz.
- The seed comes from the light's world position, so neighbouring torches burn out of step. It uses no random numbers, so it never touches the gameplay random stream, and the same torch looks the same on every client.
- `Ignite()`, `Extinguish()` and `SetLit(bool)` fade the light up over `fadeInSeconds` and down over `fadeOutSeconds`; once fully dark the Light component is switched off, so a put-out torch costs nothing. This is the local visual only. A future interactable light source keeps its lit state on the server and calls `SetLit` on each client when that state replicates.
- It is cosmetic and client-side. With no graphics device (dedicated server) it disables itself.
- The authored light values are the baseline; disabling the component restores them.

## Shadows

Set `Shadow Type` on the `Flame Light`'s Light component to Soft or Hard. Additional-light shadows only work in presets that enable them (Medium and above; Low and Very Low have them off), and a point light renders six shadow faces into a shared atlas, so only a few lights in view can afford them. Prefer shadows on the key torches in a room, not every sconce.

## Known gaps

- Flame particles, an emissive flame material and the torch crackle loop (`Loop_Torch`, currently a silent placeholder) are not on the prefab yet.
- `TestDungeon/Torch.prefab` references a mesh that is no longer in the project and is unused; `Torch_Lit` replaces it.
