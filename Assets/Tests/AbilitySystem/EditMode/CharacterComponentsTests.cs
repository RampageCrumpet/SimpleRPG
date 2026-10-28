using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// A character is a combatant (identity, health, life) that requires its stats and its combat loadout: the ability slots must be
    /// the same instances however often they are read, and the stats exist only between spawn and despawn.
    /// </summary>
    public class CharacterComponentsTests
    {
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp() => GameplayServices.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            GameplayServices.Reset();
        }

        private Character MakeCharacter()
        {
            GameObject go = new GameObject("character");
            created.Add(go);
            return go.AddComponent<Character>();
        }

        private AbilityDefinition MakeAbility(string name)
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.name = name;
            created.Add(ability);
            return ability;
        }

        private static void SetAbilities(Character character, params AbilityDefinition[] abilities)
        {
            typeof(CombatLoadout).GetField("abilities", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(character.Loadout, abilities.ToList());
        }

        // ----- The character requires its collaborators -----

        [Test]
        public void ACharacter_BringsItsStatsAndItsLoadout()
        {
            Character character = MakeCharacter();

            Assert.IsNotNull(character.Stats);
            Assert.IsNotNull(character.Loadout);
            Assert.AreSame(character.Stats, character.GetComponent<CharacterStats>());
            Assert.AreSame(character.Loadout, character.GetComponent<CombatLoadout>());
            Assert.AreSame(character.Stats, character.Stats, "the accessor returns the one component every time");
        }

        // ----- Ability slots are stable -----

        [Test]
        public void Slots_AreBuiltOnce_SoTheSameInstancesComeBackEveryTime()
        {
            Character character = MakeCharacter();
            SetAbilities(character, MakeAbility("First"), MakeAbility("Second"), MakeAbility("Third"));

            IReadOnlyList<IAbilitySlot> first = character.Loadout.Slots;
            IReadOnlyList<IAbilitySlot> second = character.Loadout.Slots;

            Assert.AreSame(first, second, "reading the bar does not rebuild it");
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreSame(first[i], second[i], $"slot {i} is the same instance, so its cooldown and subscribers are kept");
            }
        }

        [Test]
        public void Slots_FollowTheAbilityOrder_OneSlotPerAbility()
        {
            Character character = MakeCharacter();
            AbilityDefinition a = MakeAbility("A");
            AbilityDefinition b = MakeAbility("B");
            SetAbilities(character, a, b);

            CollectionAssert.AreEqual(new[] { a, b }, character.Loadout.Abilities);
            CollectionAssert.AreEqual(new[] { a, b }, character.Loadout.Slots.Cast<GraphAbilitySlot>().Select(s => s.Definition));
        }

        // ----- The default weapon -----

        [Test]
        public void Weapon_IsTheDefaultOne_WhenNothingIsEquipped()
        {
            Character character = MakeCharacter();
            WeaponData sword = ScriptableObject.CreateInstance<WeaponData>();
            created.Add(sword);

            Assert.IsNull(character.Loadout.Weapon, "no weapon until one is given");

            character.Loadout.DefaultWeapon = sword;

            Assert.AreSame(sword, character.Loadout.Weapon);
            Assert.AreSame(sword, character.Loadout.DefaultWeapon);
        }

        // ----- Stats exist between spawn and despawn -----

        [Test]
        public void BeforeItSpawns_ACharacterHasNoAttributes_AndNothingDerived()
        {
            CharacterStats stats = MakeCharacter().Stats;

            Assert.IsNull(stats.Attributes);
            Assert.AreEqual(0f, stats.MovementSpeed);
            Assert.IsFalse(stats.TryResolveMaxHealth(out _), "with no attributes the character's own fallback applies");
        }

        [Test]
        public void Initialize_CreatesAndRegistersTheAttributes_AndReleaseRemovesThem()
        {
            CharacterStats stats = MakeCharacter().Stats;
            EntityId id = new EntityId(42);
            SourceHandle source = SourceHandle.FromEntity(id);

            stats.Initialize(id);

            Assert.IsNotNull(stats.Attributes);
            Assert.IsTrue(GameplayServices.Current.Attributes.TryGetSet(source, out AttributeSet registered));
            Assert.AreSame(stats.Attributes, registered, "abilities and worn items find the attributes under the character's identity");

            stats.Release(id);

            Assert.IsNull(stats.Attributes);
            Assert.IsFalse(GameplayServices.Current.Attributes.TryGetSet(source, out _));
        }

        [Test]
        public void ChangingAnAttribute_RaisesModified_UntilTheStatsAreReleased()
        {
            CharacterStats stats = MakeCharacter().Stats;
            EntityId id = new EntityId(7);
            int modified = 0;
            stats.Modified += () => modified++;
            stats.Initialize(id);

            stats.Attributes.SetBase(PrimaryAttributes.Strength, 25);
            Assert.Greater(modified, 0, "what depends on a derived stat (maximum health) hears about the change");

            int before = modified;
            AttributeSet released = stats.Attributes;
            stats.Release(id);
            released.SetBase(PrimaryAttributes.Strength, 30);
            Assert.AreEqual(before, modified, "a released character no longer reports changes");
        }
    }
}
