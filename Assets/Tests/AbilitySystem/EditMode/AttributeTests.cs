using NUnit.Framework;
using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    public class AttributeSetTests
    {
        private static readonly AttributeId MagicPower = new AttributeId("test.attr.magicpower");
        private static readonly AttributeId MaxHealth = new AttributeId("test.attr.maxhealth");

        private static readonly SourceHandle Amulet = SourceHandle.FromItem(new ItemInstanceId(1));
        private static readonly SourceHandle Ring = SourceHandle.FromItem(new ItemInstanceId(2));

        [Test]
        public void PrimaryAttributes_AreTheSixDistinctOnes()
        {
            Assert.AreEqual(6, PrimaryAttributes.All.Length);
            CollectionAssert.AllItemsAreUnique(PrimaryAttributes.All);
        }

        [Test]
        public void UnsetAttribute_ReadsZero()
        {
            Assert.AreEqual(0d, new AttributeSet().Get(PrimaryAttributes.Strength));
        }

        [Test]
        public void BaseValue_IsReadBack()
        {
            AttributeSet set = new AttributeSet();

            set.SetBase(PrimaryAttributes.Strength, 12);

            Assert.AreEqual(12d, set.Get(PrimaryAttributes.Strength));
        }

        [Test]
        public void Layers_ApplyAddThenPercentThenMultiply()
        {
            AttributeSet set = new AttributeSet();
            set.SetBase(PrimaryAttributes.Strength, 10);
            set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 5));
            set.AddModifier(AttributeModifier.Percent(PrimaryAttributes.Strength, 0.1));
            set.AddModifier(AttributeModifier.Percent(PrimaryAttributes.Strength, 0.1));
            set.AddModifier(AttributeModifier.Factor(PrimaryAttributes.Strength, 2));

            // (10 + 5) * (1 + 0.2) * 2
            Assert.AreEqual(36d, set.Get(PrimaryAttributes.Strength), 1e-9);
        }

        [Test]
        public void Modifier_OnlyAffectsItsOwnAttribute()
        {
            AttributeSet set = new AttributeSet();
            set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 5));

            Assert.AreEqual(0d, set.Get(PrimaryAttributes.Constitution));
        }

        [Test]
        public void RemovingOneModifier_LeavesIdenticalOnesFromOtherSources()
        {
            AttributeSet set = new AttributeSet();
            ModifierHandle fromAmulet = set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 3, Amulet));
            set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 3, Ring));

            Assert.IsTrue(set.RemoveModifier(fromAmulet));

            Assert.AreEqual(3d, set.Get(PrimaryAttributes.Strength));
        }

        [Test]
        public void RemovingAHandleTwice_ReportsFalseAndChangesNothing()
        {
            AttributeSet set = new AttributeSet();
            ModifierHandle handle = set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 3));
            set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 4));

            Assert.IsTrue(set.RemoveModifier(handle));
            Assert.IsFalse(set.RemoveModifier(handle));
            Assert.AreEqual(4d, set.Get(PrimaryAttributes.Strength));
        }

        [Test]
        public void EquipUnequipCycles_DoNotDrift()
        {
            AttributeSet set = new AttributeSet();
            set.SetBase(PrimaryAttributes.Strength, 10);

            for (int i = 0; i < 100; i++)
            {
                ModifierHandle handle = set.AddModifier(AttributeModifier.Percent(PrimaryAttributes.Strength, 0.1, Amulet));
                set.RemoveModifier(handle);
            }

            Assert.AreEqual(10d, set.Get(PrimaryAttributes.Strength));
            Assert.AreEqual(0, set.ModifierCount);
        }

        [Test]
        public void RemoveAllFromSource_WithdrawsOnlyThatSourcesContributions()
        {
            AttributeSet set = new AttributeSet();
            set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 3, Amulet));
            set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Constitution, 4, Amulet));
            set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 5, Ring));

            Assert.AreEqual(2, set.RemoveAllFromSource(Amulet));

            Assert.AreEqual(5d, set.Get(PrimaryAttributes.Strength));
            Assert.AreEqual(0d, set.Get(PrimaryAttributes.Constitution));
            Assert.AreEqual(0, set.RemoveAllFromSource(Amulet));
        }

        [Test]
        public void Version_ChangesOnlyWhenSomethingChanges()
        {
            AttributeSet set = new AttributeSet();
            int start = set.Version;

            set.Get(PrimaryAttributes.Strength);
            Assert.AreEqual(start, set.Version);

            ModifierHandle handle = set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 1));
            Assert.AreNotEqual(start, set.Version);

            int afterAdd = set.Version;
            set.RemoveModifier(new ModifierHandle(999));
            Assert.AreEqual(afterAdd, set.Version);

            set.RemoveModifier(handle);
            Assert.AreNotEqual(afterAdd, set.Version);
        }

        [Test]
        public void ReadAfterChange_IsNotServedFromAStaleCache()
        {
            AttributeSet set = new AttributeSet();
            set.SetBase(PrimaryAttributes.Strength, 10);
            Assert.AreEqual(10d, set.Get(PrimaryAttributes.Strength));

            set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, 5));

            Assert.AreEqual(15d, set.Get(PrimaryAttributes.Strength));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Modifier_RejectsNonFiniteValues(double value)
        {
            AttributeSet set = new AttributeSet();

            Assert.Throws<ArgumentException>(() => set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Strength, value)));
        }

        [Test]
        public void Modifier_RejectsNoAttribute()
        {
            AttributeSet set = new AttributeSet();

            Assert.Throws<ArgumentException>(() => set.AddModifier(AttributeModifier.Flat(default, 1)));
        }

        [Test]
        public void Derived_IsComputedFromOtherAttributes()
        {
            AttributeSchema schema = new AttributeSchema().Define(MagicPower, a => 12 + 0.8 * a.Get(PrimaryAttributes.Intelligence));
            AttributeSet set = new AttributeSet(schema);
            set.SetBase(PrimaryAttributes.Intelligence, 10);

            Assert.AreEqual(20d, set.Get(MagicPower), 1e-9);
        }

        [Test]
        public void Derived_FollowsChangesToItsInputs()
        {
            AttributeSchema schema = new AttributeSchema().Define(MaxHealth, a => 50 + 10 * a.Get(PrimaryAttributes.Constitution));
            AttributeSet set = new AttributeSet(schema);
            set.SetBase(PrimaryAttributes.Constitution, 5);
            Assert.AreEqual(100d, set.Get(MaxHealth));

            ModifierHandle handle = set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Constitution, 3, Amulet));
            Assert.AreEqual(130d, set.Get(MaxHealth));

            set.RemoveModifier(handle);
            Assert.AreEqual(100d, set.Get(MaxHealth));
        }

        [Test]
        public void Derived_CanItselfBeModified()
        {
            AttributeSchema schema = new AttributeSchema().Define(MagicPower, a => 10);
            AttributeSet set = new AttributeSet(schema);
            set.AddModifier(AttributeModifier.Percent(MagicPower, 0.5));

            Assert.AreEqual(15d, set.Get(MagicPower), 1e-9);
        }

        [Test]
        public void Derived_CanReadAnotherDerived()
        {
            AttributeId spellDamage = new AttributeId("test.attr.spelldamage");
            AttributeSchema schema = new AttributeSchema()
                .Define(MagicPower, a => 2 * a.Get(PrimaryAttributes.Intelligence))
                .Define(spellDamage, a => a.Get(MagicPower) + 1);
            AttributeSet set = new AttributeSet(schema);
            set.SetBase(PrimaryAttributes.Intelligence, 5);

            Assert.AreEqual(11d, set.Get(spellDamage));
        }

        [Test]
        public void Derived_DependingOnItself_ThrowsAndStaysUsable()
        {
            AttributeId a = new AttributeId("test.attr.a");
            AttributeId b = new AttributeId("test.attr.b");
            AttributeSchema schema = new AttributeSchema()
                .Define(a, r => r.Get(b))
                .Define(b, r => r.Get(a))
                .Define(MagicPower, r => 7);
            AttributeSet set = new AttributeSet(schema);

            Assert.Throws<InvalidOperationException>(() => set.Get(a));
            Assert.AreEqual(7d, set.Get(MagicPower));
        }

        [Test]
        public void Derived_NonFiniteResult_ReadsZero()
        {
            AttributeSchema schema = new AttributeSchema().Define(MagicPower, a => 1.0 / a.Get(PrimaryAttributes.Willpower));
            AttributeSet set = new AttributeSet(schema);

            Assert.AreEqual(0d, set.Get(MagicPower));
        }

        [Test]
        public void Schema_RejectsPrimaryDuplicateAndEmptyDefinitions()
        {
            AttributeSchema schema = new AttributeSchema().Define(MagicPower, a => 1);

            Assert.Throws<ArgumentException>(() => schema.Define(PrimaryAttributes.Strength, a => 1));
            Assert.Throws<ArgumentException>(() => schema.Define(MagicPower, a => 2));
            Assert.Throws<ArgumentException>(() => schema.Define(default, a => 2));
            Assert.Throws<ArgumentNullException>(() => schema.Define(MaxHealth, null));
        }

        [Test]
        public void AttributeId_EqualityIsByKey()
        {
            Assert.AreEqual(new AttributeId("x.y"), new AttributeId("x.y"));
            Assert.AreNotEqual(new AttributeId("x.y"), new AttributeId("x.z"));
            Assert.Throws<ArgumentException>(() => new AttributeId(" "));
        }
    }

    public class AttributeBindingTests
    {
        private static readonly AttributeId MagicPower = new AttributeId("test.attr.magicpower");

        private sealed class Lookup : IAttributeLookup
        {
            private readonly Dictionary<SourceHandle, AttributeSet> sets = new Dictionary<SourceHandle, AttributeSet>();

            public void Add(SourceHandle source, AttributeSet set) => sets[source] = set;

            public bool TryGetAttributes(SourceHandle source, out IAttributeReader attributes)
            {
                bool found = sets.TryGetValue(source, out AttributeSet set);
                attributes = set;
                return found;
            }
        }

        private static readonly EntityId Wizard = new EntityId(1);
        private static readonly EntityId Monster = new EntityId(2);
        private static readonly SourceHandle Staff = SourceHandle.FromItem(new ItemInstanceId(10));
        private static readonly SourceHandle Brazier = SourceHandle.FromWorldObject(20);

        private static AttributeSet SetWith(double magicPower)
        {
            AttributeSet set = new AttributeSet();
            set.SetBase(MagicPower, magicPower);
            return set;
        }

        [Test]
        public void SameBinding_ReadsWhicheverInstigatorRuns_TheAbility()
        {
            Lookup lookup = new Lookup();
            lookup.Add(SourceHandle.FromEntity(Wizard), SetWith(30));
            lookup.Add(SourceHandle.FromEntity(Monster), SetWith(8));
            AttributeBinding binding = AttributeBinding.FromInstigator(MagicPower);

            Assert.IsTrue(binding.TryResolve(lookup, SourceHandle.FromEntity(Wizard), Wizard, out double wizardValue));
            Assert.IsTrue(binding.TryResolve(lookup, SourceHandle.FromEntity(Monster), Monster, out double monsterValue));

            Assert.AreEqual(30d, wizardValue);
            Assert.AreEqual(8d, monsterValue);
        }

        [Test]
        public void SourceBinding_ReadsTheItemsFixedStat_NotTheWielders()
        {
            Lookup lookup = new Lookup();
            lookup.Add(SourceHandle.FromEntity(Wizard), SetWith(30));
            lookup.Add(Staff, SetWith(18));
            AttributeBinding binding = AttributeBinding.FromSource(MagicPower);

            Assert.IsTrue(binding.TryResolve(lookup, Staff, Wizard, out double value));

            Assert.AreEqual(18d, value);
        }

        [Test]
        public void ConstantBinding_NeedsNoLookupAndNoInstigator()
        {
            AttributeBinding binding = AttributeBinding.Constant(5);

            Assert.IsTrue(binding.TryResolve(null, Brazier, default, out double value));

            Assert.AreEqual(5d, value);
        }

        [Test]
        public void InstigatorBinding_WithNoInstigator_UsesTheFallback()
        {
            Lookup lookup = new Lookup();
            AttributeBinding binding = AttributeBinding.FromInstigator(MagicPower, fallback: 4);

            Assert.IsFalse(binding.TryResolve(lookup, Brazier, default, out double value));

            Assert.AreEqual(4d, value);
        }

        [Test]
        public void SourceBinding_WithASourceThatHasNoAttributes_UsesTheFallback()
        {
            Lookup lookup = new Lookup();
            AttributeBinding binding = AttributeBinding.FromSource(MagicPower, fallback: 2);

            Assert.IsFalse(binding.TryResolve(lookup, Brazier, Wizard, out double value));

            Assert.AreEqual(2d, value);
        }

        [Test]
        public void Binding_WithNoLookup_UsesTheFallback()
        {
            AttributeBinding binding = AttributeBinding.FromInstigator(MagicPower, fallback: 3);

            Assert.IsFalse(binding.TryResolve(null, Brazier, Wizard, out double value));

            Assert.AreEqual(3d, value);
        }

        [Test]
        public void InstigatorBinding_SeesLiveChanges()
        {
            Lookup lookup = new Lookup();
            AttributeSet wizard = SetWith(30);
            lookup.Add(SourceHandle.FromEntity(Wizard), wizard);
            AttributeBinding binding = AttributeBinding.FromInstigator(MagicPower);

            wizard.AddModifier(AttributeModifier.Flat(MagicPower, 5));
            binding.TryResolve(lookup, default, Wizard, out double value);

            Assert.AreEqual(35d, value);
        }
    }
}
