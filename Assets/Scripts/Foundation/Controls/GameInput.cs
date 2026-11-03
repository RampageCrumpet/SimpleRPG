using UnityEngine;
using UnityEngine.InputSystem;

namespace SimpleRPG.Controls
{
    /// <summary>
    /// Single entry point for player input. Every binding lives in the GameInput actions asset, so a key is changed by
    /// rebinding an action (<see cref="SaveBindingOverrides"/> persists the change) and nothing reads a hard-coded key.
    /// </summary>
    public static class GameInput
    {
        private const string ResourceName = "GameInput";
        private const string MapName = "Gameplay";
        private const string OverridesPlayerPrefsKey = "SimpleRPG.ControlsBindingOverrides";

        public const string MoveName = "Move";
        public const string LookName = "Look";
        public const string InteractName = "Interact";
        public const string InventoryName = "Inventory";
        public const string CancelName = "Cancel";
        private const string AbilityNamePrefix = "Ability";

        private static InputActionAsset asset;
        private static InputActionMap gameplay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            asset = null;
            gameplay = null;
        }

        /// <summary>
        /// The live, enabled actions asset. This is a private copy, so rebinding never edits the project asset.
        /// </summary>
        public static InputActionAsset Asset
        {
            get
            {
                if (asset == null) Load();
                return asset;
            }
        }

        public static InputAction Move => Gameplay.FindAction(MoveName, true);
        public static InputAction Look => Gameplay.FindAction(LookName, true);
        public static InputAction Interact => Gameplay.FindAction(InteractName, true);
        public static InputAction Inventory => Gameplay.FindAction(InventoryName, true);
        public static InputAction Cancel => Gameplay.FindAction(CancelName, true);

        /// <summary>
        /// The action for the one-based ability slot, or null if no action is defined for it.
        /// </summary>
        public static InputAction Ability(int slotNumber) => Gameplay.FindAction(AbilityNamePrefix + slotNumber);

        /// <summary>
        /// The pointer position in screen pixels, or zero when there is no mouse.
        /// </summary>
        public static Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        /// <summary>
        /// Text naming the control currently bound to the action, e.g. "F".
        /// </summary>
        public static string BindingDisplay(InputAction action)
        {
            // Read from the binding paths, not the resolved controls, so a rebind shows up immediately.
            var names = new System.Collections.Generic.List<string>();
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite) continue;
                names.Add(InputControlPath.ToHumanReadableString(binding.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice));
            }

            return string.Join(" / ", names);
        }

        /// <summary>
        /// Saves the player's rebinds so they survive a restart.
        /// </summary>
        public static void SaveBindingOverrides()
        {
            PlayerPrefs.SetString(OverridesPlayerPrefsKey, Asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Restores every binding to the asset default and forgets the saved rebinds.
        /// </summary>
        public static void ResetBindingOverrides()
        {
            Asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OverridesPlayerPrefsKey);
        }

        private static InputActionMap Gameplay
        {
            get
            {
                if (gameplay == null) Load();
                return gameplay;
            }
        }

        private static void Load()
        {
            InputActionAsset template = Resources.Load<InputActionAsset>(ResourceName);
            if (template == null)
            {
                throw new System.InvalidOperationException($"Resources/{ResourceName}.inputactions is missing.");
            }

            asset = Object.Instantiate(template);

            string overrides = PlayerPrefs.GetString(OverridesPlayerPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(overrides))
            {
                asset.LoadBindingOverridesFromJson(overrides);
            }

            gameplay = asset.FindActionMap(MapName, true);
            gameplay.Enable();
        }
    }
}
