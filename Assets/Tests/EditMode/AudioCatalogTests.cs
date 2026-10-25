using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Audio;
using SimpleRPG.AudioTools;
using SimpleRPG.InventorySystem;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The shipped audio configuration: the mixer and routing asset, the example catalog and the longsword's swing sounds. These read assets,
    /// so they prove the configuration is consistent and resolves as designed, not that any of it sounds right.
    /// </summary>
    public sealed class AudioCatalogTests
    {
        private const string Catalog = "Assets/Audio/Catalog/";

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(Catalog + path);
            Assert.IsNotNull(asset, "missing catalog asset " + path + "; run SimpleRPG > Audio > Create Example Catalog");
            return asset;
        }

        [Test]
        public void EveryAudioAssetOrComponentClassLivesInAFileOfTheSameName()
        {
            // Unity binds a saved asset or a prefab's component to its script by file. A class in another class's file loses its script reference
            // when the project reloads, which silently breaks every asset made from it.
            foreach (System.Type type in typeof(AudioService).Assembly.GetTypes())
            {
                if (type.Namespace != "SimpleRPG.Audio" || type.IsAbstract || !(typeof(ScriptableObject).IsAssignableFrom(type) || typeof(MonoBehaviour).IsAssignableFrom(type)))
                {
                    continue;
                }

                bool found = false;
                foreach (string guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
                {
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                    found |= script != null && script.GetClass() == type;
                }

                Assert.IsTrue(found, type.Name + " needs its own file named " + type.Name + ".cs");
            }
        }

        [Test]
        public void NoAudioDefinitionInTheProjectHasAnError()
        {
            AudioValidationReport report = AudioValidator.ValidateAll();
            Assert.IsEmpty(report.Errors, string.Join("\n", report.Errors));
        }

        [Test]
        public void RoutingHasAMixerGroupAndAnExposedVolumeForEveryCategory()
        {
            AudioRouting.ResetCache();
            AudioRouting routing = AudioRouting.Instance;
            Assert.IsNotNull(routing, "no AudioRouting asset in Resources");
            Assert.IsNotNull(routing.Mixer);
            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
            {
                Assert.IsNotNull(routing.GroupFor(category), category + " has no mixer group");
                Assert.IsTrue(routing.Mixer.GetFloat(AudioRouting.VolumeParameter(category), out _), category + " volume is not exposed");
            }
        }

        [Test]
        public void StoneFootstepsDependOnFootwearAndFallBackToBareFeet()
        {
            AudioEventTable table = Load<AudioEventTable>("Tables/Footsteps_Default.asset");
            AcousticSurface stone = Load<AcousticSurface>("Surfaces/Surface_Stone.asset");
            AcousticSurface gravel = Load<AcousticSurface>("Surfaces/Surface_Gravel.asset");
            AudioTag bare = Load<AudioTag>("Tags/Footwear_Barefoot.asset");
            AudioTag boots = Load<AudioTag>("Tags/Footwear_Boots.asset");

            Assert.AreEqual("Footstep_Stone_Bare", table.Resolve(new AudioContext { Surface = stone, Footwear = bare }).name);
            Assert.AreEqual("Footstep_Stone_Boots", table.Resolve(new AudioContext { Surface = stone, Footwear = boots }).name);
            Assert.AreEqual("Footstep_Stone_Bare", table.Resolve(new AudioContext { Surface = gravel, Footwear = boots }).name, "a surface with no row uses the table's fallback");
        }

        [Test]
        public void SteelOnBoneImpactsStepUpForTheHead()
        {
            AudioEventTable table = Load<AudioEventTable>("Tables/Impacts_Default.asset");
            AudioTag steel = Load<AudioTag>("Tags/Material_Steel.asset");
            AudioTag bone = Load<AudioTag>("Tags/Material_Bone.asset");

            Assert.AreEqual("Impact_SteelOnBone", table.Resolve(new AudioContext { Source = steel, Target = bone, Region = AudioRegion.Torso }).name);
            Assert.AreEqual("Impact_SteelOnBone_Head", table.Resolve(new AudioContext { Source = steel, Target = bone, Region = AudioRegion.Head }).name);
        }

        [Test]
        public void GravelFallsBackThroughDirtToGround()
        {
            AcousticSurface gravel = Load<AcousticSurface>("Surfaces/Surface_Gravel.asset");
            AcousticSurface dirt = Load<AcousticSurface>("Surfaces/Surface_Dirt.asset");
            AcousticSurface ground = Load<AcousticSurface>("Surfaces/Surface_Ground.asset");
            AcousticSurface water = Load<AcousticSurface>("Surfaces/Surface_Water.asset");

            Assert.AreEqual(1, gravel.DistanceTo(dirt));
            Assert.AreEqual(2, gravel.DistanceTo(ground));
            Assert.AreEqual(-1, gravel.DistanceTo(water));
        }

        [Test]
        public void EachLongswordAttackHasItsOwnSwingSound()
        {
            ComboDefinition combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>("Assets/Items/Weapon Data/Longsword_Combo.asset");
            Assert.IsNotNull(combo);
            Assert.AreEqual(3, combo.StepCount);

            string[] expected = { "Swing_Longsword_A1", "Swing_Longsword_A2", "Swing_Longsword_A3" };
            for (int i = 0; i < 3; i++)
            {
                Assert.IsNotNull(combo.Steps[i].SwingSound, "attack " + (i + 1) + " has no swing sound");
                Assert.AreEqual(expected[i], combo.Steps[i].SwingSound.name);
                Assert.Less(combo.Steps[i].SwingSoundTime, combo.Steps[i].Duration, "the swing sound must start inside the step");
                Assert.IsTrue(combo.Steps[i].SwingSound.Resolve(default).HasContent, "swing " + (i + 1) + " has no recording");
            }

            Assert.IsEmpty(combo.Validate(), "the combo's own validation should still pass");
        }

        [Test]
        public void ShippedPrefabsCarryTheAudioComponentsAndTheirAssets()
        {
            GameObject player = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Characters/Player/TestCharacter.prefab");
            GameObject skeleton = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab");
            try
            {
                foreach (GameObject character in new[] { player, skeleton })
                {
                    Assert.IsNotNull(character.GetComponent<CombatAudio>(), character.name + " needs CombatAudio");
                    Assert.IsNotNull(character.GetComponent<FootstepEmitter>(), character.name + " needs FootstepEmitter");
                    Assert.IsNotNull(character.GetComponent<MovementAudioState>(), character.name + " needs MovementAudioState");
                    Assert.IsNotNull(character.GetComponent<AudioIdentity>().BodyMaterial, character.name + " needs a body material");
                }

                Assert.IsNotNull(player.GetComponent<EquipmentMovementAudio>());
                Assert.AreEqual("Material_Bone", skeleton.GetComponent<AudioIdentity>().BodyMaterial.name);
                Assert.AreEqual("Footwear_Barefoot", skeleton.GetComponent<MovementAudioState>().Footwear.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
                PrefabUtility.UnloadPrefabContents(skeleton);
            }
        }

        [Test]
        public void SkeletonSwingsAndFootstepsAreAudioEventsOnItsClips()
        {
            const string folder = "Assets/Prefabs/Characters/Enemies/";
            foreach (string clipName in new[] { "Skeleton_Overhead", "Skeleton_Slash" })
            {
                AnimationEvent swing = null;
                foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + clipName + ".anim")))
                {
                    if (e.functionName == "PlayEvent") swing = e;
                    Assert.AreNotEqual("PlayClip", e.functionName, clipName + " should play its swing through the audio system");
                }

                Assert.IsNotNull(swing, clipName + " has no swing sound event");
                Assert.IsInstanceOf<AudioEventDefinition>(swing.objectReferenceParameter);
                Assert.IsTrue(((AudioEventDefinition)swing.objectReferenceParameter).HasContent);
            }

            int footDowns = 0;
            foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Skeleton_Walk.anim")))
            {
                if (e.functionName == "OnFootDown") footDowns++;
            }

            Assert.AreEqual(2, footDowns);
        }

        [Test]
        public void GemItemsHaveInventorySoundsAndTheSceneHasAnAmbienceController()
        {
            Item ruby = AssetDatabase.LoadAssetAtPath<Item>("Assets/Items/LootItems/Brillaint Cut Ruby.asset");
            Assert.IsNotNull(InventoryAudio.SoundFor(ruby, InventoryAudioAction.Place));

            // Read the scene file rather than the open scene: the test runner may have another scene loaded.
            string guid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Audio/AmbienceController.cs");
            string sceneText = System.IO.File.ReadAllText("Assets/Scenes/SampleScene.unity");
            Assert.IsTrue(sceneText.Contains(guid), "SampleScene needs an AmbienceController");
        }

        [Test]
        public void ExampleCatalogCoversTheSevenRequiredCases()
        {
            // Weapon swing, bone impact, stone footstep with footwear, metal rattle, gem placement, spatial loop, intermittent ambience.
            Assert.IsNotNull(Load<AudioEventDefinition>("Events/Swing_Longsword_A1.asset"));
            Assert.IsNotNull(Load<AudioEventDefinition>("Events/Impact_SteelOnBone.asset"));
            Assert.IsNotNull(Load<AudioEventTable>("Tables/Footsteps_Default.asset"));
            Assert.IsNotNull(Load<MovementAudioProfile>("Profiles/Movement_PlateArmor.asset").StepSound);
            Assert.IsNotNull(Load<ItemAudioProfile>("Profiles/ItemAudio_Gems.asset").Get(InventoryAudioAction.Place));

            AudioEventDefinition torch = Load<AudioEventDefinition>("Events/Loop_Torch.asset");
            Assert.AreEqual(AudioPlaybackMode.Sustained, torch.Mode);
            Assert.AreEqual(AudioSpatialMode.World3D, torch.Spatial);

            AmbienceProfile crypt = Load<AmbienceProfile>("Profiles/Ambience_Crypt.asset");
            Assert.AreEqual(1, crypt.Intermittent.Count);
            Assert.Greater(crypt.QuietChance, 0f);
        }
    }
}
