using SimpleRPG.Abilities.Integration;
using SimpleRPG.Gameplay;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Gives test weapons a combo and lets a test move a character's combo time by hand, so hit windows open and close when the test says.
    /// Every step of the test combo lasts 1 s, with its hit window at 0.2-0.5 s and (except the last step) its chain window at 0.6-0.9 s.
    /// Damage scalars are 1, 0.8 and 0.9. The manual clock ticks 30 times a second, so times are in whole ticks.
    /// </summary>
    internal static class ComboTestSupport
    {
        public const double InsideHitWindow = 0.3;
        public const double InsideChainWindow = 0.7;

        /// <summary>
        /// Creates the three-step test combo. The caller owns it and must destroy it.
        /// </summary>
        public static ComboDefinition CreateCombo()
        {
            return ComboDefinition.Create(new[]
            {
                new ComboStep("A1", null, 1f, 0.2f, 0.5f, 0.6f, 0.9f, damageScalar: 1f),
                new ComboStep("A2", null, 1f, 0.2f, 0.5f, 0.6f, 0.9f, damageScalar: 0.8f),
                new ComboStep("A3", null, 1f, 0.2f, 0.5f, damageScalar: 0.9f),
            });
        }

        /// <summary>
        /// Sets a weapon's combo. Weapons are immutable assets in the game, so a test has to reach in to give a freshly created one a combo.
        /// </summary>
        public static void GiveCombo(WeaponData weapon, ComboDefinition combo)
        {
            FieldInfo field = typeof(WeaponData).GetField("combo", BindingFlags.Instance | BindingFlags.NonPublic);
            Debug.Assert(field != null, "WeaponData should have a combo field.");
            field.SetValue(weapon, combo);
        }

        /// <summary>
        /// Puts a character's combo on a clock the test controls, starting at zero.
        /// </summary>
        public static ManualGameplayClock UseManualClock(Character character)
        {
            ManualGameplayClock clock = new ManualGameplayClock();
            character.GetComponent<ComboMeleeDelivery>().Clock = clock;
            return clock;
        }

        /// <summary>
        /// Moves a character's combo time forward and lets the combo catch up, as the next frame's update would.
        /// </summary>
        public static void Advance(Character character, double seconds)
        {
            ComboMeleeDelivery host = character.GetComponent<ComboMeleeDelivery>();
            ManualGameplayClock clock = (ManualGameplayClock)host.Clock;
            clock.AdvanceSeconds(seconds);
            MethodInfo update = typeof(ComboMeleeDelivery).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            update.Invoke(host, null);
        }
    }
}
