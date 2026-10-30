using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleRPG.Audio;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Runs the playback service in the real engine: voice pooling, limits, sustained start and stop, attachment to transforms, spatial and
    /// mixer settings, surface queries, footstep assembly and ambience. They read the state the service sets on real AudioSources and its own
    /// counters, with a fake clock for time. They do not prove how anything sounds, how loud distance attenuation is, or that a device
    /// plays the audio; that needs a person listening.
    /// </summary>
    public sealed class AudioServicePlayModeTests
    {
        private readonly List<Object> created = new List<Object>();
        private AudioService service;
        private float now;
        private bool? previousHeadless;

        [SetUp]
        public void SetUp()
        {
            if (AudioService.Current != null) Object.DestroyImmediate(AudioService.Current.gameObject);
            previousHeadless = AudioService.HeadlessOverride;
            AudioService.HeadlessOverride = false;
            AudioRouting.ResetCache();
            Track(new GameObject("Listener")).AddComponent<AudioListener>();
            now = 100f;
            service = Make(AudioRouting.Instance, 8);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            AudioRouting.ResetCache();
            AudioService.HeadlessOverride = previousHeadless;
        }

        private AudioService Make(AudioRouting routing, int voices)
        {
            if (service != null)
            {
                Object.DestroyImmediate(service.gameObject);
            }

            GameObject host = new GameObject("TestAudioService");
            created.Add(host);
            AudioService made = host.AddComponent<AudioService>();
            made.Configure(routing, voices);
            made.SetTestHooks(new SystemAudioRandom(1), () => now);
            return made;
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private AudioClip Clip(float seconds = 1f) => Track(AudioClip.Create("clip", Mathf.Max(1, (int)(44100 * seconds)), 1, 44100, false));

        private AudioEventDefinition OneShot(string name, int maxConcurrent = 4, int importance = 50, AudioLimitBehavior full = AudioLimitBehavior.StopOldest, float interval = 0f, AudioSpatialMode spatial = AudioSpatialMode.World3D, AudioCategory category = AudioCategory.World, int layers = 1)
        {
            AudioLayer[] list = new AudioLayer[layers];
            for (int i = 0; i < layers; i++) list[i] = AudioEventDefinition.Layer(Clip());
            return Track(AudioEventDefinition.Create(name, category, AudioPlaybackMode.OneShot, list, spatial, maxConcurrent, importance, full, interval));
        }

        private AudioEventDefinition Loop(string name, AudioCategory category = AudioCategory.Ambience, AudioSpatialMode spatial = AudioSpatialMode.World3D)
        {
            return Track(AudioEventDefinition.Create(name, category, AudioPlaybackMode.Sustained, new[] { AudioEventDefinition.Layer(Clip(), loop: true) }, spatial, 4, 50, AudioLimitBehavior.StopOldest, 0f, 1f, 0f));
        }

        private AudioSource[] ActiveSources() => service.GetComponentsInChildren<AudioSource>(false);

        [Test]
        public void OneShotPlaysThenIsReleasedAndItsVoiceIsReused()
        {
            AudioEventDefinition ev = OneShot("Hit");
            AudioHandle first = service.Play(ev, AudioPlacement.At(Vector3.zero));
            Assert.IsTrue(first.IsValid);
            Assert.AreEqual(1, service.ActiveCount);
            Assert.AreEqual(1, service.VoicesInUse);
            int created = service.VoicesCreated;

            now += 2f;
            service.Tick(now);
            Assert.AreEqual(0, service.ActiveCount, "a finished one-shot is released");
            Assert.AreEqual(0, service.VoicesInUse);
            Assert.IsFalse(first.IsPlaying, "the old handle no longer refers to anything");

            for (int i = 0; i < 5; i++)
            {
                service.Play(ev, AudioPlacement.At(Vector3.zero));
                now += 2f;
                service.Tick(now);
            }

            Assert.AreEqual(created, service.VoicesCreated, "repeated sounds reuse the same voice instead of making objects");
            Assert.AreEqual(0, ActiveSources().Length, "idle voices are switched off");
        }

        [Test]
        public void PerEventConcurrencyCanStopTheOldestOrDropTheNew()
        {
            AudioEventDefinition stealer = OneShot("Steal", maxConcurrent: 2, full: AudioLimitBehavior.StopOldest);
            service.Play(stealer, AudioPlacement.At(Vector3.zero));
            now += 0.1f;
            AudioHandle second = service.Play(stealer, AudioPlacement.At(Vector3.zero));
            now += 0.1f;
            AudioHandle third = service.Play(stealer, AudioPlacement.At(Vector3.zero));
            Assert.IsTrue(third.IsValid);
            Assert.AreEqual(2, service.ActiveCount, "a third copy replaces the oldest");
            Assert.IsTrue(second.IsPlaying);

            AudioEventDefinition dropper = OneShot("Drop", maxConcurrent: 1, full: AudioLimitBehavior.DropNew);
            Assert.IsTrue(service.Play(dropper, AudioPlacement.At(Vector3.zero)).IsValid);
            Assert.IsFalse(service.Play(dropper, AudioPlacement.At(Vector3.zero)).IsValid, "while it plays, a repeat is ignored");
        }

        [Test]
        public void MinimumIntervalStopsABurstOfIdenticalRequests()
        {
            AudioEventDefinition ev = OneShot("Burst", maxConcurrent: 0, interval: 0.2f);
            Assert.IsTrue(service.Play(ev, AudioPlacement.None).IsValid);
            now += 0.05f;
            Assert.IsFalse(service.Play(ev, AudioPlacement.None).IsValid);
            now += 0.2f;
            Assert.IsTrue(service.Play(ev, AudioPlacement.None).IsValid);
        }

        [Test]
        public void GlobalVoiceLimitCutsOffTheLeastImportantAndNeverExceedsTheCap()
        {
            service = Make(null, 4);
            AudioEventDefinition minor = OneShot("Minor", maxConcurrent: 0, importance: 10);
            AudioEventDefinition major = OneShot("Major", maxConcurrent: 0, importance: 90);

            for (int i = 0; i < 4; i++)
            {
                now += 0.01f;
                service.Play(minor, AudioPlacement.None);
            }

            Assert.AreEqual(4, service.VoicesInUse);
            Assert.IsTrue(service.Play(major, AudioPlacement.None).IsValid, "an important sound takes a voice from a minor one");
            Assert.LessOrEqual(service.VoicesCreated, 4);
            Assert.LessOrEqual(service.VoicesInUse, 4);

            for (int i = 0; i < 4; i++)
            {
                now += 0.01f;
                service.Play(major, AudioPlacement.None);
            }

            Assert.IsFalse(service.Play(minor, AudioPlacement.None).IsValid, "a minor sound cannot push out important ones");
            Assert.LessOrEqual(service.VoicesInUse, 4);
        }

        [Test]
        public void LayeredEventUsesOneVoicePerLayerAndReleasesAllOfThem()
        {
            AudioEventDefinition layered = OneShot("Layered", layers: 3);
            service.Play(layered, AudioPlacement.None);
            Assert.AreEqual(1, service.ActiveCount);
            Assert.AreEqual(3, service.VoicesInUse);

            now += 2f;
            service.Tick(now);
            Assert.AreEqual(0, service.VoicesInUse);
        }

        [Test]
        public void SustainedSoundLoopsUntilStoppedThenReleasesItsVoices()
        {
            AudioEventDefinition hum = Loop("Hum");
            AudioHandle handle = service.Play(hum, AudioPlacement.At(Vector3.one));
            Assert.IsTrue(handle.IsPlaying);
            Assert.IsTrue(ActiveSources()[0].loop, "a sustained layer loops");

            now += 60f;
            service.Tick(now);
            Assert.IsTrue(handle.IsPlaying, "a loop does not end by itself");

            handle.Stop(fade: false);
            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0, service.ActiveCount);
            Assert.AreEqual(0, service.VoicesInUse);
            Assert.AreEqual(0, ActiveSources().Length);
            Assert.DoesNotThrow(() => handle.Stop(), "stopping again is harmless");
        }

        [Test]
        public void SustainedSoundFadesOutOverItsFadeTime()
        {
            AudioEventDefinition hum = Track(AudioEventDefinition.Create("Fade", AudioCategory.Magic, AudioPlaybackMode.Sustained,
                new[] { AudioEventDefinition.Layer(Clip(), loop: true) }, fadeOutSeconds: 1f));
            AudioHandle handle = service.Play(hum, AudioPlacement.None);
            handle.Stop();
            Assert.IsFalse(handle.IsPlaying, "it is already on its way out");
            Assert.AreEqual(1, service.ActiveCount);

            now += 0.5f;
            service.Tick(now);
            Assert.AreEqual(0.5f, ActiveSources()[0].volume, 0.05f);

            now += 0.6f;
            service.Tick(now);
            Assert.AreEqual(0, service.ActiveCount, "after the fade the voice is released");
        }

        [Test]
        public void AttachedSoundFollowsItsTransformAndALoopEndsWhenTheOwnerIsDestroyed()
        {
            GameObject owner = Track(new GameObject("Projectile"));
            owner.transform.position = new Vector3(1f, 0f, 0f);
            AudioHandle flight = service.Play(Loop("Flight", AudioCategory.Magic), AudioPlacement.Attached(owner.transform));
            service.Tick(now);
            Assert.AreEqual(1f, ActiveSources()[0].transform.position.x, 1e-4f);

            owner.transform.position = new Vector3(7f, 0f, 0f);
            service.Tick(now);
            Assert.AreEqual(7f, ActiveSources()[0].transform.position.x, 1e-4f, "the sound moves with its owner");

            Object.DestroyImmediate(owner);
            service.Tick(now);
            service.Tick(now + 1f);
            Assert.IsFalse(flight.IsPlaying, "a loop whose owner is gone must not keep playing");
            Assert.AreEqual(0, service.VoicesInUse);
        }

        [Test]
        public void LoopEmitterStartsWithItsObjectAndStopsWhenItIsDisabledOrDestroyed()
        {
            AudioEventDefinition torch = Loop("Torch");
            GameObject prop = Track(new GameObject("Torch"));
            prop.SetActive(false);
            AudioLoopEmitter emitter = prop.AddComponent<AudioLoopEmitter>();
            emitter.Sound = torch;
            prop.SetActive(true);
            Assert.IsTrue(emitter.IsPlaying);
            Assert.AreEqual(1, service.ActiveCount);

            prop.SetActive(false);
            Assert.AreEqual(0, service.ActiveCount, "disabling the owner ends the loop");

            prop.SetActive(true);
            Assert.AreEqual(1, service.ActiveCount);
            Object.DestroyImmediate(prop);
            Assert.AreEqual(0, service.ActiveCount, "destroying the owner ends the loop");
        }

        [Test]
        public void SpatialSettingsAndNonSpatialModeReachTheAudioSource()
        {
            AudioEventDefinition ev = OneShot("Spatial", spatial: AudioSpatialMode.World3D);
            service.Play(ev, AudioPlacement.At(new Vector3(3f, 0f, 0f)));
            AudioSource source = ActiveSources()[0];
            Assert.AreEqual(1f, source.spatialBlend);
            Assert.AreEqual(ev.MinDistance, source.minDistance);
            Assert.AreEqual(ev.MaxDistance, source.maxDistance);
            Assert.AreEqual(ev.Rolloff, source.rolloffMode);
            Assert.AreEqual(3f, source.transform.position.x, 1e-4f);

            service.StopAll();
            AudioEventDefinition ui = OneShot("Ui", spatial: AudioSpatialMode.NonSpatial, category: AudioCategory.UI);
            service.Play(ui, AudioPlacement.None);
            Assert.AreEqual(0f, ActiveSources()[0].spatialBlend, "non-spatial sounds are heard the same everywhere");
        }

        [Test]
        public void VoicesAreRoutedToTheirCategorysMixerGroup()
        {
            AudioRouting routing = AudioRouting.Instance;
            Assume.That(routing, Is.Not.Null, "the project has no AudioRouting asset in Resources; run SimpleRPG > Audio > Create Mixer And Routing");
            service = Make(routing, 8);

            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
            {
                service.StopAll();
                Assert.IsNotNull(routing.GroupFor(category), category + " has no mixer group in the routing asset");
                service.Play(OneShot("Routed" + category, category: category), AudioPlacement.None);
                Assert.AreSame(routing.GroupFor(category), ActiveSources()[0].outputAudioMixerGroup, category + " plays on the wrong mixer group");
            }
        }

        [Test]
        public void MixerCategoryVolumeParameterIsExposedAndSettable()
        {
            AudioRouting routing = AudioRouting.Instance;
            Assume.That(routing, Is.Not.Null);
            Assume.That(routing.Mixer, Is.Not.Null);
            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
            {
                Assert.IsTrue(routing.SetCategoryVolumeDb(category, -6f), AudioRouting.VolumeParameter(category) + " is not exposed on the mixer");
                Assert.IsTrue(routing.Mixer.GetFloat(AudioRouting.VolumeParameter(category), out float value));
                Assert.AreEqual(-6f, value, 0.01f);
                routing.SetCategoryVolumeDb(category, 0f);
            }
        }

        [Test]
        public void MissingClipsOrNullEventsStartNothingAndLeakNothing()
        {
            AudioEventDefinition empty = Track(AudioEventDefinition.Create("Empty", AudioCategory.World, AudioPlaybackMode.OneShot, new[] { new AudioLayer() }));
            Assert.IsFalse(service.Play(empty, AudioPlacement.None).IsValid);
            Assert.IsFalse(service.Play((AudioEventDefinition)null, AudioPlacement.None).IsValid);
            Assert.IsFalse(service.Play((AudioEventBase)null, default, AudioPlacement.None).IsValid);
            Assert.AreEqual(0, service.ActiveCount);
            Assert.AreEqual(0, service.VoicesInUse);
        }

        [Test]
        public void WithNoAudioListenerNothingIsStartedAndTheGameIsNotSpammed()
        {
            Object.DestroyImmediate(Object.FindAnyObjectByType<AudioListener>().gameObject);
            now += 5f;
            AudioHandle handle = service.Play(OneShot("Unheard"), AudioPlacement.None);
            Assert.IsFalse(handle.IsValid, "no listener, nothing can be heard");
            Assert.AreEqual(0, service.ActiveCount);

            Track(new GameObject("Listener")).AddComponent<AudioListener>();
            now += 2f;
            Assert.IsTrue(service.Play(OneShot("Heard"), AudioPlacement.None).IsValid, "playing resumes once a listener exists");
        }

        // ----- Surfaces -----

        private GameObject Ground(string name, AcousticSurface surface, Vector3 center, bool trigger = false, float topY = 0f)
        {
            GameObject ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.name = name;
            ground.transform.position = center + Vector3.up * (topY - 0.5f);
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            ground.GetComponent<Collider>().isTrigger = trigger;
            if (surface != null)
            {
                ground.AddComponent<AcousticSurfaceSource>().Surface = surface;
            }

            Physics.SyncTransforms();
            return ground;
        }

        [Test]
        public void SurfaceQueryFindsTheGroundBelowAPointAndLayersOverlaysOnIt()
        {
            AudioRouting.Override(Track(AudioRouting.Create(null, null, 8)));
            AcousticSurface stone = Track(AudioTag.Create<AcousticSurface>("Stone"));
            AcousticSurface water = Track(AudioTag.Create<AcousticSurface>("Water"));
            Ground("Floor", stone, Vector3.zero);
            Ground("Puddle", water, Vector3.zero, trigger: true, topY: 0.1f);

            SurfaceSample sample = SurfaceQuery.Below(new Vector3(0f, 0.05f, 0f), 1f, ~0);
            Assert.AreSame(stone, sample.Surface, "the solid collider is the ground");
            Assert.AreSame(water, sample.Overlay, "shallow water over stone keeps both");

            SurfaceSample dry = SurfaceQuery.Below(new Vector3(30f, 0.05f, 0f), 1f, ~0);
            Assert.IsNull(dry.Surface);
            Assert.IsNull(dry.Overlay);
        }

        [Test]
        public void SurfaceFallsBackToPhysicsMaterialThenToTheDefaultThenToNone()
        {
            AcousticSurface wood = Track(AudioTag.Create<AcousticSurface>("Wood"));
            AcousticSurface fallbackSurface = Track(AudioTag.Create<AcousticSurface>("Default"));
            PhysicsMaterial woodMaterial = Track(new PhysicsMaterial("WoodMat"));
            AudioRouting routing = Track(AudioRouting.Create(null, null, 8, defaultSurface: fallbackSurface,
                physicsSurfaces: new[] { new AudioRouting.PhysicsMaterialSurface { material = woodMaterial, surface = wood } }));
            AudioRouting.Override(routing);

            GameObject plank = Ground("Plank", null, Vector3.zero);
            plank.GetComponent<Collider>().sharedMaterial = woodMaterial;
            Assert.AreSame(wood, SurfaceQuery.FromCollider(plank.GetComponent<Collider>()).Surface, "no surface component: the physics material decides");

            GameObject bare = Ground("Bare", null, new Vector3(50f, 0f, 0f));
            Assert.AreSame(fallbackSurface, SurfaceQuery.FromCollider(bare.GetComponent<Collider>()).Surface, "no metadata at all: the project default");

            AudioRouting.Override(Track(AudioRouting.Create(null, null, 8)));
            Assert.IsNull(SurfaceQuery.FromCollider(null).Surface, "no collider, no routing default: no surface");
        }

        [Test]
        public void TheWalkersOwnCollidersAreNotTheGround()
        {
            AcousticSurface stone = Track(AudioTag.Create<AcousticSurface>("Stone"));
            Ground("Floor", stone, Vector3.zero);
            GameObject walker = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            walker.transform.position = new Vector3(0f, 1f, 0f);
            walker.AddComponent<AcousticSurfaceSource>().Surface = Track(AudioTag.Create<AcousticSurface>("Self"));
            Physics.SyncTransforms();

            SurfaceSample sample = SurfaceQuery.Below(new Vector3(0f, 1.5f, 0f), 3f, ~0, walker.transform);
            Assert.AreSame(stone, sample.Surface);
        }

        // ----- Footsteps -----

        [Test]
        public void FootstepPlaysSurfaceSoundOverlayObjectCreakAndEquipmentLayer()
        {
            AcousticSurface wood = Track(AudioTag.Create<AcousticSurface>("Wood"));
            AcousticSurface water = Track(AudioTag.Create<AcousticSurface>("Water"));
            AudioTag boots = Track(AudioTag.Create("Boots"));
            AudioEventDefinition woodBoot = OneShot("WoodBoot");
            AudioEventDefinition splash = OneShot("Splash");
            AudioEventDefinition creak = OneShot("Creak");
            AudioEventDefinition clink = OneShot("Clink");
            AudioEventTable table = Track(AudioEventTable.Create("Steps", null,
                AudioEventTable.Row(woodBoot, new AudioCondition { facet = AudioFacet.Surface, tag = wood }, new AudioCondition { facet = AudioFacet.Footwear, tag = boots }),
                AudioEventTable.Row(splash, new AudioCondition { facet = AudioFacet.Surface, tag = water })));

            GameObject bridge = Track(new GameObject("Bridge"));
            AcousticSurfaceSource bridgeSource = bridge.AddComponent<AcousticSurfaceSource>();
            bridgeSource.Surface = wood;
            bridgeSource.ContactSounds = new[] { new ContactSound { sound = creak, kinds = AudioContactKind.Footstep, chance = 1f } };

            GameObject walker = Track(new GameObject("Walker"));
            MovementAudioState state = walker.AddComponent<MovementAudioState>();
            state.SetSlot(1, MovementAudioProfile.Create("Boots", footwear: boots));
            state.SetSlot(2, MovementAudioProfile.Create("Plate", stepSound: clink, chance: 1f));
            FootstepEmitter emitter = walker.AddComponent<FootstepEmitter>();
            emitter.Footsteps = table;

            SurfaceSample sample = new SurfaceSample { Surface = wood, Overlay = water, BaseSource = bridgeSource };
            emitter.Step(Vector3.zero, sample, 0.8f, now);

            // Wood-and-boots footstep, the splash over it, the bridge's creak and the plate's clink.
            Assert.AreEqual(4, service.ActiveCount);
        }

        [Test]
        public void FootstepWithNoTableOrSurfaceIsSilentNotAnError()
        {
            AudioRouting.Override(Track(AudioRouting.Create(null, null, 8)));
            GameObject walker = Track(new GameObject("Walker"));
            FootstepEmitter emitter = walker.AddComponent<FootstepEmitter>();
            Assert.DoesNotThrow(() => emitter.Step(Vector3.zero, default, 0.5f, now));
            Assert.AreEqual(0, service.ActiveCount);
        }

        // ----- Ambience -----

        [Test]
        public void AmbienceStartsBedsStopsThemOnChangeAndPlaysIntermittentSoundsAroundTheListener()
        {
            AudioEventDefinition bed = Loop("Bed", AudioCategory.Ambience, AudioSpatialMode.NonSpatial);
            AudioEventDefinition drip = OneShot("Drip", category: AudioCategory.Ambience);
            AmbienceProfile crypt = Track(AmbienceProfile.Create("Crypt", new AudioEventBase[] { bed },
                new[] { new IntermittentSound { sound = drip, minInterval = 5f, maxInterval = 5f, minDistance = 4f, maxDistance = 4f, chance = 1f } }));

            GameObject host = Track(new GameObject("Ambience"));
            host.SetActive(false);
            AmbienceController controller = host.AddComponent<AmbienceController>();
            controller.DefaultProfile = crypt;
            host.SetActive(true);

            Assert.AreSame(crypt, controller.Active);
            Assert.AreEqual(1, service.ActiveCount, "the bed is playing");
            Assert.AreEqual(0f, ActiveSources()[0].spatialBlend, "ambience beds are not positional");

            controller.Tick(Time.unscaledTime + 6f);
            Assert.AreEqual(2, service.ActiveCount, "the due intermittent sound plays");

            controller.SetProfile(null);
            service.Tick(now + 1f);
            Assert.AreEqual(1, service.ActiveCount, "changing area ends the bed (the one-shot drip finishes by itself)");
            now += 5f;
            service.Tick(now);
            Assert.AreEqual(0, service.ActiveCount);
        }

        [Test]
        public void ZonesPickTheProfileForTheListenersPosition()
        {
            AmbienceProfile cellar = Track(AmbienceProfile.Create("Cellar", null, null));
            GameObject room = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            room.transform.position = new Vector3(100f, 0f, 0f);
            room.GetComponent<Collider>().isTrigger = true;
            AmbienceZone zone = room.AddComponent<AmbienceZone>();
            zone.Profile = cellar;
            Physics.SyncTransforms();

            Assert.AreSame(zone, AmbienceZone.At(new Vector3(100f, 0f, 0f)));
            Assert.IsNull(AmbienceZone.At(new Vector3(0f, 0f, 0f)));
        }

        // ----- Frequent play does not allocate scene objects -----

        [UnityTest]
        public IEnumerator FrequentPlayDoesNotCreateGameObjectsAfterWarmUp()
        {
            AudioEventDefinition step = OneShot("Step", maxConcurrent: 0);
            service.Play(step, AudioPlacement.None);
            service.Play(step, AudioPlacement.None);
            now += 2f;
            service.Tick(now);
            int voices = service.VoicesCreated;
            int objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;

            for (int i = 0; i < 50; i++)
            {
                service.Play(step, AudioPlacement.At(Vector3.forward * i));
                now += 0.05f;
                service.Tick(now);
                yield return null;
            }

            Assert.AreEqual(voices, service.VoicesCreated, "no new voices beyond what bursts needed");
            Assert.LessOrEqual(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length, objects + 4);
        }
    }
}
