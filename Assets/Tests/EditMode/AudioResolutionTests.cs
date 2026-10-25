using System.Collections.Generic;
using HitDetection;
using NUnit.Framework;
using SimpleRPG.Audio;
using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>Returns a fixed sequence of values, then repeats the last.</summary>
    internal sealed class ScriptedRandom : IAudioRandom
    {
        private readonly float[] values;
        private int next;

        public ScriptedRandom(params float[] values) => this.values = values.Length > 0 ? values : new[] { 0f };

        public float Value() => values[Mathf.Min(next++, values.Length - 1)];
    }

    /// <summary>
    /// Audio selection logic: which event a context resolves to, which clip a layer picks, and how invalid or missing content behaves.
    /// These check the decisions only; nothing here proves how a sound actually sounds.
    /// </summary>
    public sealed class AudioResolutionTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private AudioEventDefinition Event(string name, params AudioClip[] clips)
        {
            AudioLayer layer = new AudioLayer();
            List<WeightedClip> list = new List<WeightedClip>();
            foreach (AudioClip clip in clips) list.Add(new WeightedClip(clip));
            layer.clips = list.ToArray();
            return Track(AudioEventDefinition.Create(name, AudioCategory.World, AudioPlaybackMode.OneShot, new[] { layer }));
        }

        private AudioClip Clip(string name)
        {
            return Track(AudioClip.Create(name, 100, 1, 44100, false));
        }

        private static AudioCondition On(AudioFacet facet, AudioTag tag) => new AudioCondition { facet = facet, tag = tag };

        [Test]
        public void TableMostSpecificRowWinsWhateverTheOrder()
        {
            AudioTag stone = Track(AudioTag.Create("Stone"));
            AudioTag boots = Track(AudioTag.Create("Boots"));
            AudioEventDefinition plain = Event("StonePlain", Clip("a"));
            AudioEventDefinition booted = Event("StoneBoots", Clip("b"));

            // The less specific row is listed last on purpose.
            AudioEventTable table = Track(AudioEventTable.Create("Steps", null,
                AudioEventTable.Row(booted, On(AudioFacet.Surface, stone), On(AudioFacet.Footwear, boots)),
                AudioEventTable.Row(plain, On(AudioFacet.Surface, stone))));

            Assert.AreSame(booted, table.Resolve(new AudioContext { Surface = stone, Footwear = boots }));
            Assert.AreSame(plain, table.Resolve(new AudioContext { Surface = stone }));
        }

        [Test]
        public void TableFallsBackUpTheTagChainThenToTheFallbackEvent()
        {
            AudioTag ground = Track(AudioTag.Create("Ground"));
            AudioTag dirt = Track(AudioTag.Create("Dirt", ground));
            AudioTag gravel = Track(AudioTag.Create("Gravel", dirt));
            AudioTag metal = Track(AudioTag.Create("Metal"));
            AudioEventDefinition dirtStep = Event("Dirt", Clip("d"));
            AudioEventDefinition generic = Event("Generic", Clip("g"));
            AudioEventTable table = Track(AudioEventTable.Create("Steps", generic, AudioEventTable.Row(dirtStep, On(AudioFacet.Surface, dirt))));

            Assert.AreSame(dirtStep, table.Resolve(new AudioContext { Surface = gravel }), "gravel has no row of its own, so it uses dirt's");
            Assert.AreSame(generic, table.Resolve(new AudioContext { Surface = metal }), "an unrelated surface uses the fallback");
            Assert.AreSame(generic, table.Resolve(default), "no surface at all uses the fallback");
        }

        [Test]
        public void ExactTagBeatsAnAncestorMatch()
        {
            AudioTag dirt = Track(AudioTag.Create("Dirt"));
            AudioTag gravel = Track(AudioTag.Create("Gravel", dirt));
            AudioEventDefinition dirtStep = Event("Dirt", Clip("d"));
            AudioEventDefinition gravelStep = Event("Gravel", Clip("g"));
            AudioEventTable table = Track(AudioEventTable.Create("Steps", null,
                AudioEventTable.Row(dirtStep, On(AudioFacet.Surface, dirt)),
                AudioEventTable.Row(gravelStep, On(AudioFacet.Surface, gravel))));

            Assert.AreSame(gravelStep, table.Resolve(new AudioContext { Surface = gravel }));
        }

        [Test]
        public void TableHonorsRegionAndIntensityRanges()
        {
            AudioTag sword = Track(AudioTag.Create("Steel"));
            AudioEventDefinition any = Event("Body", Clip("a"));
            AudioEventDefinition head = Event("Head", Clip("b"));
            AudioEventDefinition hard = Event("Hard", Clip("c"));
            AudioTableEntry headRow = AudioEventTable.Row(head, On(AudioFacet.Source, sword));
            headRow.regions = AudioRegion.Head;
            AudioTableEntry hardRow = AudioEventTable.Row(hard, On(AudioFacet.Source, sword));
            hardRow.minIntensity = 0.8f;
            AudioEventTable table = Track(AudioEventTable.Create("Impacts", null, AudioEventTable.Row(any, On(AudioFacet.Source, sword)), headRow, hardRow));

            Assert.AreSame(head, table.Resolve(new AudioContext { Source = sword, Region = AudioRegion.Head, Intensity = 0.2f }));
            Assert.AreSame(any, table.Resolve(new AudioContext { Source = sword, Region = AudioRegion.Legs, Intensity = 0.2f }));
            Assert.AreSame(hard, table.Resolve(new AudioContext { Source = sword, Region = AudioRegion.Legs, Intensity = 0.9f }));
        }

        [Test]
        public void SourceAndTargetMaterialsSelectTheImpactFamily()
        {
            AudioTag steel = Track(AudioTag.Create("Steel"));
            AudioTag bone = Track(AudioTag.Create("Bone"));
            AudioTag flesh = Track(AudioTag.Create("Flesh"));
            AudioEventDefinition metalOnBone = Event("MetalOnBone", Clip("a"));
            AudioEventDefinition metalOnFlesh = Event("MetalOnFlesh", Clip("b"));
            AudioEventTable table = Track(AudioEventTable.Create("Impacts", null,
                AudioEventTable.Row(metalOnBone, On(AudioFacet.Source, steel), On(AudioFacet.Target, bone)),
                AudioEventTable.Row(metalOnFlesh, On(AudioFacet.Source, steel), On(AudioFacet.Target, flesh))));

            Assert.AreSame(metalOnBone, table.Resolve(new AudioContext { Source = steel, Target = bone }));
            Assert.AreSame(metalOnFlesh, table.Resolve(new AudioContext { Source = steel, Target = flesh }));
        }

        [Test]
        public void WeaponOverrideBeatsTargetOverrideBeatsSharedTable()
        {
            AudioEventDefinition weapon = Event("W", Clip("a"));
            AudioEventDefinition target = Event("T", Clip("b"));
            AudioEventDefinition shared = Event("S", Clip("c"));

            Assert.AreSame(weapon, CombatAudio.SelectImpactSound(weapon, target, shared));
            Assert.AreSame(target, CombatAudio.SelectImpactSound(null, target, shared));
            Assert.AreSame(shared, CombatAudio.SelectImpactSound(null, null, shared));
            Assert.IsNull(CombatAudio.SelectImpactSound(null, null, null));
        }

        [Test]
        public void NestedTablesResolveButACycleEndsInSilence()
        {
            AudioEventDefinition leaf = Event("Leaf", Clip("a"));
            AudioEventTable inner = Track(AudioEventTable.Create("Inner", leaf));
            AudioEventTable outer = Track(AudioEventTable.Create("Outer", inner));
            Assert.AreSame(leaf, outer.Resolve(default));

            AudioEventTable a = Track(AudioEventTable.Create("A", null));
            AudioEventTable b = Track(AudioEventTable.Create("B", a));
            typeof(AudioEventTable).GetField("fallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(a, b);
            Assert.IsNull(a.Resolve(default), "a table that refers back to itself must stop, not recurse forever");
        }

        [Test]
        public void MissingEntriesAndNullRequestsResolveToSilence()
        {
            AudioEventTable empty = Track(AudioEventTable.Create("Empty", null));
            Assert.IsNull(empty.Resolve(default));

            AudioEventTable holes = Track(AudioEventTable.Create("Holes", null, new AudioTableEntry { result = null }, null));
            Assert.IsNull(holes.Resolve(default));
        }

        [Test]
        public void PlannerPicksByWeightWithoutRepeatingTheLastClip()
        {
            AudioClip a = Clip("a");
            AudioClip b = Clip("b");
            AudioClip c = Clip("c");
            AudioLayer layer = new AudioLayer { clips = new[] { new WeightedClip(a, 1f), new WeightedClip(b, 1f), new WeightedClip(c, 1f) } };
            AudioEventDefinition def = Track(AudioEventDefinition.Create("Var", AudioCategory.World, AudioPlaybackMode.OneShot, new[] { layer }));
            VoicePlanner planner = new VoicePlanner();
            IAudioRandom random = new SystemAudioRandom(7);
            List<PlannedVoice> plan = new List<PlannedVoice>();

            AudioClip previous = null;
            HashSet<AudioClip> seen = new HashSet<AudioClip>();
            for (int i = 0; i < 60; i++)
            {
                plan.Clear();
                Assert.AreEqual(1, planner.Plan(def, random, plan));
                Assert.AreNotSame(previous, plan[0].Clip, "the same variation must not play twice in a row");
                previous = plan[0].Clip;
                seen.Add(previous);
            }

            Assert.AreEqual(3, seen.Count, "every variation should come up");
        }

        [Test]
        public void PlannerRespectsWeightsAndSkipsMissingClips()
        {
            AudioClip heavy = Clip("heavy");
            AudioClip light = Clip("light");
            AudioLayer layer = new AudioLayer { clips = new[] { new WeightedClip(heavy, 9f), new WeightedClip(null, 50f), new WeightedClip(light, 1f), new WeightedClip(Clip("zero"), 0f) } };
            AudioEventDefinition def = Track(AudioEventDefinition.Create("W", AudioCategory.World, AudioPlaybackMode.OneShot, new[] { layer }));
            VoicePlanner planner = new VoicePlanner();
            IAudioRandom random = new SystemAudioRandom(3);
            List<PlannedVoice> plan = new List<PlannedVoice>();

            int heavyCount = 0;
            int total = 400;
            for (int i = 0; i < total; i++)
            {
                plan.Clear();
                planner.Plan(def, random, plan);
                Assert.AreEqual(1, plan.Count);
                Assert.IsNotNull(plan[0].Clip, "a missing clip slot must never be chosen");
                Assert.AreNotEqual("zero", plan[0].Clip.name, "a zero weight must never be chosen");
                if (plan[0].Clip == heavy) heavyCount++;
            }

            // No repeats means the heavy clip alternates with the light one, so it cannot dominate; both must appear.
            Assert.Greater(heavyCount, 0);
            Assert.Less(heavyCount, total);
        }

        [Test]
        public void PlannerAppliesChancesAndKeepsVariationInRange()
        {
            AudioLayer always = new AudioLayer { clips = new[] { new WeightedClip(Clip("a")) }, pitch = 1f, pitchVariationSemitones = 2f, volume = 0.5f, volumeVariation = 0.2f };
            AudioLayer rare = new AudioLayer { clips = new[] { new WeightedClip(Clip("b")) }, chance = 0.25f };
            AudioEventDefinition def = Track(AudioEventDefinition.Create("L", AudioCategory.Combat, AudioPlaybackMode.OneShot, new[] { always, rare }));
            VoicePlanner planner = new VoicePlanner();
            List<PlannedVoice> plan = new List<PlannedVoice>();

            // Rolls in order: clip pick, volume, pitch for layer 1, then the rare layer's chance (0.9 fails it).
            plan.Clear();
            Assert.AreEqual(1, planner.Plan(def, new ScriptedRandom(0.5f, 0.5f, 0.5f, 0.9f), plan));
            Assert.AreEqual(1f, plan[0].Pitch, 1e-4f, "a mid roll means no pitch change");
            Assert.AreEqual(0.5f, plan[0].Volume, 1e-4f);

            plan.Clear();
            Assert.AreEqual(2, planner.Plan(def, new ScriptedRandom(0.5f, 0.5f, 0.5f, 0.1f, 0.5f), plan), "the rare layer joins when its roll is under its chance");

            IAudioRandom random = new SystemAudioRandom(11);
            for (int i = 0; i < 200; i++)
            {
                plan.Clear();
                planner.Plan(def, random, plan);
                float semitones = 12f * Mathf.Log(plan[0].Pitch, 2f);
                Assert.LessOrEqual(Mathf.Abs(semitones), 2.001f);
                Assert.That(plan[0].Volume, Is.InRange(0.4f - 1e-4f, 0.6f + 1e-4f));
            }
        }

        [Test]
        public void EventChanceCanSilenceTheWholeEvent()
        {
            AudioEventDefinition def = Track(AudioEventDefinition.Create("C", AudioCategory.World, AudioPlaybackMode.OneShot,
                new[] { AudioEventDefinition.Layer(Clip("a")) }, chance: 0.3f));
            List<PlannedVoice> plan = new List<PlannedVoice>();
            Assert.AreEqual(0, new VoicePlanner().Plan(def, new ScriptedRandom(0.9f), plan));
            Assert.AreEqual(1, new VoicePlanner().Plan(def, new ScriptedRandom(0.1f), plan));
        }

        [Test]
        public void PlannerOnlyLoopsLayersOfSustainedEvents()
        {
            AudioLayer looping = AudioEventDefinition.Layer(Clip("a"), loop: true);
            AudioEventDefinition sustained = Track(AudioEventDefinition.Create("S", AudioCategory.Ambience, AudioPlaybackMode.Sustained, new[] { looping }));
            AudioEventDefinition oneShot = Track(AudioEventDefinition.Create("O", AudioCategory.Ambience, AudioPlaybackMode.OneShot, new[] { looping }));
            List<PlannedVoice> plan = new List<PlannedVoice>();

            new VoicePlanner().Plan(sustained, new ScriptedRandom(0f), plan);
            Assert.IsTrue(plan[0].Loop);
            plan.Clear();
            new VoicePlanner().Plan(oneShot, new ScriptedRandom(0f), plan);
            Assert.IsFalse(plan[0].Loop, "a loop in a one-shot would never end, so it is not looped");
        }

        [Test]
        public void MissingContentPlansNothingAndNeverThrows()
        {
            List<PlannedVoice> plan = new List<PlannedVoice>();
            VoicePlanner planner = new VoicePlanner();
            Assert.AreEqual(0, planner.Plan(null, new ScriptedRandom(0f), plan));

            AudioEventDefinition noClips = Event("Empty");
            Assert.AreEqual(0, planner.Plan(noClips, new ScriptedRandom(0f), plan));

            AudioEventDefinition nullLayers = Track(AudioEventDefinition.Create("Null", AudioCategory.World, AudioPlaybackMode.OneShot, new AudioLayer[] { null }));
            Assert.AreEqual(0, planner.Plan(nullLayers, new ScriptedRandom(0f), plan));

            AudioEventDefinition noLayerArray = Track(AudioEventDefinition.Create("NoArray", AudioCategory.World, AudioPlaybackMode.OneShot, null));
            Assert.AreEqual(0, planner.Plan(noLayerArray, new ScriptedRandom(0f), plan));
        }

        [Test]
        public void ValidationFlagsMissingClipsAndInvalidSettings()
        {
            AudioValidationReport silent = new AudioValidationReport();
            Event("Silent").Validate(silent);
            Assert.IsEmpty(silent.Errors);
            Assert.IsNotEmpty(silent.Warnings, "an event with no clip is silent, which deserves a warning");

            AudioValidationReport loopInOneShot = new AudioValidationReport();
            Track(AudioEventDefinition.Create("Bad", AudioCategory.World, AudioPlaybackMode.OneShot, new[] { AudioEventDefinition.Layer(Clip("a"), loop: true) })).Validate(loopInOneShot);
            Assert.IsNotEmpty(loopInOneShot.Errors);

            AudioValidationReport noLayers = new AudioValidationReport();
            Track(AudioEventDefinition.Create("None", AudioCategory.World, AudioPlaybackMode.OneShot, new AudioLayer[0])).Validate(noLayers);
            Assert.IsNotEmpty(noLayers.Errors);

            AudioValidationReport sustainedNoLoop = new AudioValidationReport();
            Track(AudioEventDefinition.Create("Ends", AudioCategory.World, AudioPlaybackMode.Sustained, new[] { AudioEventDefinition.Layer(Clip("a")) })).Validate(sustainedNoLoop);
            Assert.IsNotEmpty(sustainedNoLoop.Warnings);

            AudioValidationReport good = new AudioValidationReport();
            Event("Good", Clip("a")).Validate(good);
            Assert.IsTrue(good.IsClean);
        }

        [Test]
        public void TableValidationFlagsHolesAndSelfReference()
        {
            AudioEventTable table = Track(AudioEventTable.Create("T", null, new AudioTableEntry { result = null }));
            AudioValidationReport report = new AudioValidationReport();
            table.Validate(report);
            Assert.IsNotEmpty(report.Errors);

            AudioTag tag = Track(AudioTag.Create("X"));
            AudioEventTable selfish = Track(AudioEventTable.Create("Self", null));
            typeof(AudioEventTable).GetField("entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(selfish, new[] { AudioEventTable.Row(selfish, On(AudioFacet.Surface, tag)) });
            AudioValidationReport selfReport = new AudioValidationReport();
            selfish.Validate(selfReport);
            Assert.IsNotEmpty(selfReport.Errors);
        }

        // ----- Footwear and equipment layering -----

        [Test]
        public void FootstepContextCarriesSurfaceFootwearArmorAndIntensity()
        {
            GameObject host = Track(new GameObject("Walker"));
            MovementAudioState state = host.AddComponent<MovementAudioState>();
            AudioTag boots = Track(AudioTag.Create("Boots"));
            AudioTag plate = Track(AudioTag.Create("Plate"));
            AudioTag stone = Track(AudioTag.Create("Stone"));
            state.SetSlot((int)EquipmentSlot.feet, MovementAudioProfile.Create("Boots", footwear: boots));
            state.SetSlot((int)EquipmentSlot.chest, MovementAudioProfile.Create("Plate", armor: plate));

            AudioContext context = FootstepEmitter.BuildContext(stone, state, 0.7f);
            Assert.AreSame(stone, context.Surface);
            Assert.AreSame(boots, context.Footwear);
            Assert.AreSame(plate, context.Armor);
            Assert.AreEqual(0.7f, context.Intensity);
        }

        [Test]
        public void FootwearChangesTheStepAndBareFeetUseTheDefault()
        {
            GameObject host = Track(new GameObject("Walker"));
            MovementAudioState state = host.AddComponent<MovementAudioState>();
            AudioTag bare = Track(AudioTag.Create("Bare"));
            AudioTag boots = Track(AudioTag.Create("Boots"));
            AudioTag stone = Track(AudioTag.Create("Stone"));
            state.DefaultFootwear = bare;
            AudioEventDefinition bareStep = Event("BareStone", Clip("a"));
            AudioEventDefinition bootStep = Event("BootStone", Clip("b"));
            AudioEventTable table = Track(AudioEventTable.Create("Steps", null,
                AudioEventTable.Row(bareStep, On(AudioFacet.Surface, stone), On(AudioFacet.Footwear, bare)),
                AudioEventTable.Row(bootStep, On(AudioFacet.Surface, stone), On(AudioFacet.Footwear, boots))));

            Assert.AreSame(bareStep, table.Resolve(FootstepEmitter.BuildContext(stone, state, 0.5f)));
            state.SetSlot((int)EquipmentSlot.feet, MovementAudioProfile.Create("Boots", footwear: boots));
            Assert.AreSame(bootStep, table.Resolve(FootstepEmitter.BuildContext(stone, state, 0.5f)));
            state.SetSlot((int)EquipmentSlot.feet, null);
            Assert.AreSame(bareStep, table.Resolve(FootstepEmitter.BuildContext(stone, state, 0.5f)), "taking the boots off goes back to bare feet");
        }

        [Test]
        public void EquipmentLayersRespectGroupsCapsChanceAndCooldown()
        {
            AudioEventDefinition clink = Event("Clink", Clip("a"));
            AudioEventDefinition rustle = Event("Rustle", Clip("b"));
            AudioEventDefinition creak = Event("Creak", Clip("c"));
            MovementLayerPicker picker = new MovementLayerPicker();
            List<AudioEventBase> picked = new List<AudioEventBase>();

            MovementLayer plate = new MovementLayer { Key = 1, Sound = clink, Chance = 1f, Group = "metal" };
            MovementLayer greaves = new MovementLayer { Key = 2, Sound = clink, Chance = 1f, Group = "metal" };
            MovementLayer cloth = new MovementLayer { Key = 3, Sound = rustle, Chance = 1f };
            MovementLayer leather = new MovementLayer { Key = 4, Sound = creak, Chance = 1f };
            MovementLayer[] all = { plate, greaves, cloth, leather };

            picker.Pick(all, 0.5f, 10f, new ScriptedRandom(0f), 4, picked);
            int metal = 0;
            foreach (AudioEventBase p in picked) if (p == clink) metal++;
            Assert.AreEqual(1, metal, "two metal items share a group, so only one clinks on a step");
            Assert.AreEqual(3, picked.Count);

            picked.Clear();
            picker.Pick(all, 0.5f, 20f, new ScriptedRandom(0f), 2, picked);
            Assert.AreEqual(2, picked.Count, "the per-step cap limits the layers");

            MovementLayerPicker fresh = new MovementLayerPicker();
            picked.Clear();
            fresh.Pick(new[] { new MovementLayer { Key = 9, Sound = rustle, Chance = 0.5f } }, 0.5f, 0f, new ScriptedRandom(0f, 0.9f), 4, picked);
            Assert.IsEmpty(picked, "a failed chance roll leaves the layer out");

            MovementLayerPicker cooled = new MovementLayerPicker();
            MovementLayer[] one = { new MovementLayer { Key = 5, Sound = clink, Chance = 1f, Cooldown = 0.5f } };
            picked.Clear();
            cooled.Pick(one, 0.5f, 1f, new ScriptedRandom(0f), 4, picked);
            cooled.Pick(one, 0.5f, 1.2f, new ScriptedRandom(0f), 4, picked);
            Assert.AreEqual(1, picked.Count, "inside the cooldown the layer stays out");
            cooled.Pick(one, 0.5f, 1.6f, new ScriptedRandom(0f), 4, picked);
            Assert.AreEqual(2, picked.Count);
        }

        [Test]
        public void EquipmentLayersCanRequireAMinimumIntensity()
        {
            MovementLayerPicker picker = new MovementLayerPicker();
            List<AudioEventBase> picked = new List<AudioEventBase>();
            MovementLayer[] jangle = { new MovementLayer { Key = 1, Sound = Event("Jangle", Clip("a")), Chance = 1f, MinIntensity = 0.6f } };
            picker.Pick(jangle, 0.3f, 0f, new ScriptedRandom(0f), 4, picked);
            Assert.IsEmpty(picked, "a slow walk is too gentle to rattle the gear");
            picker.Pick(jangle, 0.9f, 1f, new ScriptedRandom(0f), 4, picked);
            Assert.AreEqual(1, picked.Count);
        }

        [Test]
        public void MovementStateOnlyOffersLayersFromItemsWithASound()
        {
            GameObject host = Track(new GameObject("Walker"));
            MovementAudioState state = host.AddComponent<MovementAudioState>();
            state.SetSlot(1, MovementAudioProfile.Create("OnlyTags", footwear: Track(AudioTag.Create("Boots"))));
            Assert.IsEmpty(state.PickStepLayers(1f, 0f, new ScriptedRandom(0f)));

            state.SetSlot(2, MovementAudioProfile.Create("Plate", stepSound: Event("Clink", Clip("a"))));
            Assert.AreEqual(1, state.PickStepLayers(1f, 1f, new ScriptedRandom(0f)).Count);

            state.ClearAll();
            Assert.IsEmpty(state.PickStepLayers(1f, 2f, new ScriptedRandom(0f)));
        }

        [Test]
        public void FootContactDetectorFiresOncePerFootfall()
        {
            FootContactDetector detector = new FootContactDetector();
            float[] heights = { 0.3f, 0.2f, 0.1f, 0.05f, 0.08f, 0.15f, 0.25f, 0.3f, 0.1f };
            int contacts = 0;
            foreach (float h in heights) if (detector.Update(h, 0.12f, 0.2f)) contacts++;
            Assert.AreEqual(2, contacts, "two descents past the contact height are two footfalls; hovering near the ground is not more");
        }

        // ----- Inventory -----

        [Test]
        public void ItemProfilesFallBackToTheirFamilyPerAction()
        {
            AudioEventDefinition crystalPlace = Event("CrystalPlace", Clip("a"));
            AudioEventDefinition crystalPickup = Event("CrystalPickup", Clip("b"));
            AudioEventDefinition rubyPlace = Event("RubyPlace", Clip("c"));
            ItemAudioProfile gems = Track(ItemAudioProfile.Create("Gems", null, crystalPickup, crystalPlace));
            ItemAudioProfile ruby = Track(ItemAudioProfile.Create("Ruby", gems, null, rubyPlace));

            Assert.AreSame(rubyPlace, ruby.Get(InventoryAudioAction.Place), "the item's own override wins");
            Assert.AreSame(crystalPickup, ruby.Get(InventoryAudioAction.Pickup), "an action the item leaves empty uses the family's");
            Assert.IsNull(ruby.Get(InventoryAudioAction.Drop), "nothing anywhere is silence");
        }

        [Test]
        public void ItemWithoutAProfileHasNoInventorySound()
        {
            Assert.IsNull(InventoryAudio.SoundFor(null, InventoryAudioAction.Place));
            Item item = Track(ScriptableObject.CreateInstance<Item>());
            Assert.IsNull(InventoryAudio.SoundFor(item, InventoryAudioAction.Place));
        }

        [Test]
        public void ItemProfileCycleDoesNotHang()
        {
            ItemAudioProfile a = Track(ItemAudioProfile.Create("A"));
            ItemAudioProfile b = Track(ItemAudioProfile.Create("B", a));
            typeof(ItemAudioProfile).GetField("parent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(a, b);
            Assert.IsNull(a.Get(InventoryAudioAction.Place));
        }

        // ----- Duplicate prevention -----

        [Test]
        public void LedgerLetsOnlyTheFirstOfTwoReportsOfOneMomentPlay()
        {
            AudioEventLedger ledger = new AudioEventLedger();
            long swing = AudioEventLedger.Key(7, 1, 2);
            Assert.IsTrue(ledger.TryClaim(swing, 10f), "the predicted swing plays");
            Assert.IsFalse(ledger.TryClaim(swing, 10.08f), "the server's confirmation of the same swing does not play again");
            Assert.IsTrue(ledger.TryClaim(AudioEventLedger.Key(7, 1, 3), 10.08f), "a different attack is a different moment");
            Assert.IsTrue(ledger.TryClaim(AudioEventLedger.Key(8, 1, 2), 10.08f), "so is another entity's");
            Assert.IsTrue(ledger.TryClaim(swing, 11f), "the same attack a second later is a new swing");
        }

        // ----- Ambience schedule -----

        [Test]
        public void IntermittentSoundsAreScheduledWithinTheirIntervalsAndQuietSpellsSuppressThem()
        {
            AudioEventDefinition drip = Event("Drip", Clip("a"));
            IntermittentSound sound = new IntermittentSound { sound = drip, minInterval = 10f, maxInterval = 20f, chance = 1f, minDistance = 3f, maxDistance = 9f };
            AmbienceProfile profile = Track(AmbienceProfile.Create("Crypt", null, new[] { sound }, quietChance: 1f, quietMin: 50f, quietMax: 50f));
            List<int> due = new List<int>();

            // Rolls: initial interval 0 (10 s), then: interval 0, quiet roll 0 (always), quiet length 0.
            AmbienceScheduler scheduler = new AmbienceScheduler(profile, new ScriptedRandom(0f), 0f);
            Assert.AreEqual(0, scheduler.Tick(9.9f, due), "not before the minimum interval");
            Assert.AreEqual(1, scheduler.Tick(10f, due));
            Assert.AreEqual(60f, scheduler.QuietUntil, 0.01f, "the quiet spell starts at the sound and lasts its configured time");

            due.Clear();
            Assert.AreEqual(0, scheduler.Tick(59f, due), "nothing plays in the quiet spell");
            Assert.AreEqual(1, scheduler.Tick(60f, due), "it resumes afterwards");
        }

        [Test]
        public void AmbienceWithNoProfileOrSoundsSchedulesNothing()
        {
            List<int> due = new List<int>();
            Assert.AreEqual(0, new AmbienceScheduler(null, new ScriptedRandom(0f), 0f).Tick(1000f, due));
            AmbienceProfile bedsOnly = Track(AmbienceProfile.Create("Beds", null, null));
            Assert.AreEqual(0, new AmbienceScheduler(bedsOnly, new ScriptedRandom(0f), 0f).Tick(1000f, due));
        }

        [Test]
        public void RegionsMapFromBodyLocations()
        {
            Assert.AreEqual(AudioRegion.Head, AudioContext.RegionOf(BodyLocation.Head));
            Assert.AreEqual(AudioRegion.Arms, AudioContext.RegionOf(BodyLocation.Arms));
            Assert.AreEqual(AudioRegion.Legs, AudioContext.RegionOf(BodyLocation.Legs));
            Assert.AreEqual(AudioRegion.Torso, AudioContext.RegionOf(BodyLocation.Torso));
        }

        // ----- Headless -----

        [Test]
        public void HeadlessSessionsPlayNothingAndCreateNoAudioObjects()
        {
            bool? previous = AudioService.HeadlessOverride;
            AudioService.HeadlessOverride = true;
            try
            {
                int before = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
                AudioEventDefinition ev = Event("Quiet", Clip("a"));
                AudioHandle handle = GameAudio.Play(ev, Vector3.zero);
                Assert.IsFalse(handle.IsValid);
                Assert.IsNull(AudioService.GetOrCreate(), "no service is made without an audio device");
                Assert.AreEqual(before, Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length);
            }
            finally
            {
                AudioService.HeadlessOverride = previous;
            }
        }

        [Test]
        public void NullAndMissingRequestsNeverThrow()
        {
            Assert.DoesNotThrow(() => GameAudio.Play(null, Vector3.zero));
            Assert.DoesNotThrow(() => GameAudio.Play2D(null));
            Assert.DoesNotThrow(() => GameAudio.PlayAttached(null, default, null));
            Assert.DoesNotThrow(() => GameAudio.Stop(default));
            Assert.DoesNotThrow(() => InteractableAudio.TryPlay(null, InteractionCue.Open));
            Assert.DoesNotThrow(() => InventoryAudio.Play(null, InventoryAudioAction.Pickup));
        }
    }
}
