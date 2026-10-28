using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SimpleRPG.HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Audio;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>A melee mechanism with no rules of its own, so tests can feed the audio the events the real one raises.</summary>
    internal sealed class StubMelee : MeleeDeliveryBase
    {
        public override AttackAnimation AnimationFor(int attackNumber) => default;

        protected override float DamageScalar(int attackNumber) => 1f;

        public override MeleePressOutcome Press(long pressTick, bool freshAllowed, int pressId = 0) => MeleePressOutcome.Rejected;

        public override void CancelPress()
        {
        }

        public override bool Begin(in MeleeSwingLaunch launch, IMeleeEvents events) => false;

        public void ShowAttack(int attackNumber, float secondsIn) => RaiseAttackStarted(attackNumber, secondsIn);

        public void CancelAttack() => RaiseAttackCancelled();

        public void Confirm(MeleeHitCue cue) => RaiseHitConfirmed(cue);
    }

    /// <summary>
    /// The combat audio adapter against a real service: swing timing from combo data, cancellation of a predicted swing, impact selection from
    /// materials and region, and duplicate suppression for a moment reported twice. Checks which clip was started and when, not how it sounds.
    /// </summary>
    public sealed class AudioCombatPlayModeTests
    {
        private readonly List<Object> created = new List<Object>();
        private AudioService service;
        private bool? previousHeadless;

        [SetUp]
        public void SetUp()
        {
            if (AudioService.Current != null) Object.DestroyImmediate(AudioService.Current.gameObject);
            previousHeadless = AudioService.HeadlessOverride;
            AudioService.HeadlessOverride = false;
            AudioRouting.ResetCache();
            Track(new GameObject("Listener")).AddComponent<AudioListener>();
            GameObject host = Track(new GameObject("TestAudioService"));
            service = host.AddComponent<AudioService>();
            service.Configure(null, 16);
            service.SetTestHooks(new SystemAudioRandom(1), () => 100f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            AudioRouting.Override(null);
            AudioRouting.ResetCache();
            AudioService.HeadlessOverride = previousHeadless;
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private AudioClip Clip(string name) => Track(AudioClip.Create(name, 44100, 1, 44100, false));

        private AudioEventDefinition Event(string name)
        {
            return Track(AudioEventDefinition.Create(name, AudioCategory.Combat, AudioPlaybackMode.OneShot, new[] { AudioEventDefinition.Layer(Clip(name)) }, maxConcurrent: 0));
        }

        private static AudioCondition On(AudioFacet facet, AudioTag tag) => new AudioCondition { facet = facet, tag = tag };

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }

        private AudioSource[] Playing() => service.GetComponentsInChildren<AudioSource>(false);

        private (CombatAudio audio, StubMelee melee, SimpleRPG.Character character) Attacker(WeaponData weapon, AudioTag defaultMaterial = null)
        {
            GameObject go = Track(new GameObject("Attacker"));
            StubMelee melee = go.AddComponent<StubMelee>();
            SimpleRPG.Character character = go.GetComponent<SimpleRPG.Character>();
            character.WeaponData = weapon;
            go.SetActive(false);
            CombatAudio audio = go.AddComponent<CombatAudio>();
            if (defaultMaterial != null) SetField(audio, "defaultSourceMaterial", defaultMaterial);
            go.SetActive(true);
            return (audio, melee, character);
        }

        private WeaponData Weapon(ComboStep step, AudioTag material = null, AudioEventBase impact = null)
        {
            WeaponData weapon = Track(ScriptableObject.CreateInstance<WeaponData>());
            SetField(weapon, "combo", Track(ComboDefinition.Create(new[] { step })));
            if (material != null) SetField(weapon, "audioMaterial", material);
            if (impact != null) SetField(weapon, "impactSound", impact);
            return weapon;
        }

        private static ComboStep Step(AudioEventBase swing, float swingTime)
        {
            return new ComboStep("Attack", null, 1.2f, 0.9f, 1.1f).WithSwingSound(swing, swingTime);
        }

        [UnityTest]
        public IEnumerator SwingSoundPlaysAtItsMarkedMomentNotWhenTheAttackStarts()
        {
            AudioEventDefinition swing = Event("Swing");
            var (_, melee, _) = Attacker(Weapon(Step(swing, 0.3f)));

            melee.ShowAttack(1, 0f);
            Assert.AreEqual(0, service.ActiveCount, "entering the attack is not the swing");

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, service.ActiveCount);
            Assert.AreSame(swing.Layers[0].clips[0].clip, Playing()[0].clip);
        }

        [UnityTest]
        public IEnumerator ASwingTheServerRefusesNeverMakesItsSound()
        {
            var (_, melee, _) = Attacker(Weapon(Step(Event("Swing"), 0.3f)));

            melee.ShowAttack(1, 0f);
            melee.CancelAttack();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0, service.ActiveCount);
        }

        [Test]
        public void AttackJoinedPartWayPlaysAtOnceIfTheMomentIsJustPastAndNotIfItIsLongGone()
        {
            var (_, melee, _) = Attacker(Weapon(Step(Event("Swing"), 0.3f)));

            melee.ShowAttack(1, 0.35f);
            Assert.AreEqual(1, service.ActiveCount, "joined just after the marked moment: still plays");

            service.StopAll();
            melee.ShowAttack(1, 0.9f);
            Assert.AreEqual(0, service.ActiveCount, "joined far later: a swing sound now would be wrong");
        }

        [Test]
        public void StepsWithoutASwingSoundAndWeaponlessAttackersAreSilent()
        {
            var (_, melee, _) = Attacker(Weapon(Step(null, 0f)));
            Assert.DoesNotThrow(() => melee.ShowAttack(1, 0f));
            Assert.DoesNotThrow(() => melee.ShowAttack(9, 0f));

            var (_, bare, _) = Attacker(null);
            Assert.DoesNotThrow(() => bare.ShowAttack(1, 0f));
            Assert.AreEqual(0, service.ActiveCount);
        }

        private (SimpleRPG.Character victim, AudioTag bone) Victim(AudioTag material)
        {
            GameObject go = Track(new GameObject("Victim"));
            go.SetActive(false);
            SimpleRPG.Character victim = go.AddComponent<SimpleRPG.Character>();
            AudioIdentity identity = go.AddComponent<AudioIdentity>();
            SetField(identity, "bodyMaterial", material);
            go.SetActive(true);
            return (victim, material);
        }

        [Test]
        public void ImpactSoundComesFromTheMaterialsAndRegionOfTheConfirmedHit()
        {
            AudioTag steel = Track(AudioTag.Create("Steel"));
            AudioTag bone = Track(AudioTag.Create("Bone"));
            AudioEventDefinition metalOnBone = Event("MetalOnBone");
            AudioEventDefinition skullCrack = Event("SkullCrack");
            AudioTableEntry head = AudioEventTable.Row(skullCrack, On(AudioFacet.Source, steel), On(AudioFacet.Target, bone));
            head.regions = AudioRegion.Head;
            AudioEventTable impacts = Track(AudioEventTable.Create("Impacts", null, AudioEventTable.Row(metalOnBone, On(AudioFacet.Source, steel), On(AudioFacet.Target, bone)), head));
            AudioRouting.Override(Track(AudioRouting.Create(null, null, 16, impacts: impacts)));

            var (audio, _, _) = Attacker(Weapon(Step(null, 0f), material: steel));
            var (victim, _) = Victim(bone);

            audio.OnHitConfirmed(new MeleeHitCue(victim, BodyLocation.Torso, 1, Vector3.up));
            Assert.AreSame(metalOnBone.Layers[0].clips[0].clip, Playing()[0].clip);

            service.StopAll();
            audio.OnHitConfirmed(new MeleeHitCue(victim, BodyLocation.Head, 2, Vector3.up));
            Assert.AreSame(skullCrack.Layers[0].clips[0].clip, Playing()[0].clip, "a head hit picks the head row of the same family");
        }

        [Test]
        public void AWeaponsOwnImpactSoundOverridesTheSharedFamily()
        {
            AudioTag steel = Track(AudioTag.Create("Steel"));
            AudioTag bone = Track(AudioTag.Create("Bone"));
            AudioEventDefinition shared = Event("Shared");
            AudioEventDefinition special = Event("Special");
            AudioRouting.Override(Track(AudioRouting.Create(null, null, 16, impacts: Track(AudioEventTable.Create("Impacts", shared)))));

            var (audio, _, _) = Attacker(Weapon(Step(null, 0f), material: steel, impact: special));
            var (victim, _) = Victim(bone);
            audio.OnHitConfirmed(new MeleeHitCue(victim, BodyLocation.Torso, 1, Vector3.zero));
            Assert.AreSame(special.Layers[0].clips[0].clip, Playing()[0].clip);
        }

        [Test]
        public void TheSameConfirmedHitReportedTwiceSoundsOnce()
        {
            AudioEventDefinition hit = Event("Hit");
            AudioRouting.Override(Track(AudioRouting.Create(null, null, 16, impacts: hit)));
            var (audio, _, _) = Attacker(Weapon(Step(null, 0f)));
            var (victim, _) = Victim(Track(AudioTag.Create("Bone")));

            MeleeHitCue cue = new MeleeHitCue(victim, BodyLocation.Torso, 1, Vector3.zero);
            audio.OnHitConfirmed(cue);
            audio.OnHitConfirmed(cue);
            Assert.AreEqual(1, service.ActiveCount, "the prediction and the confirmation of one blow are one sound");

            audio.OnHitConfirmed(new MeleeHitCue(victim, BodyLocation.Torso, 2, Vector3.zero));
            Assert.AreEqual(2, service.ActiveCount, "the next attack's hit is a different moment");
        }

        [Test]
        public void HitCueRaisedByTheDeliveryReachesTheAudioListener()
        {
            AudioEventDefinition hit = Event("Hit");
            AudioRouting.Override(Track(AudioRouting.Create(null, null, 16, impacts: hit)));
            var (_, melee, _) = Attacker(Weapon(Step(null, 0f)));
            var (victim, _) = Victim(Track(AudioTag.Create("Bone")));

            melee.Confirm(new MeleeHitCue(victim, BodyLocation.Legs, 1, Vector3.zero));
            Assert.AreEqual(1, service.ActiveCount);
        }

        [Test]
        public void HitOnATargetWithNoAudioIdentityAndNoTablesStaysSilentAndSafe()
        {
            AudioRouting.Override(Track(AudioRouting.Create(null, null, 16)));
            var (audio, _, _) = Attacker(Weapon(Step(null, 0f)));
            GameObject bare = Track(new GameObject("Bare"));
            SimpleRPG.Character victim = bare.AddComponent<SimpleRPG.Character>();

            Assert.DoesNotThrow(() => audio.OnHitConfirmed(new MeleeHitCue(victim, BodyLocation.Torso, 1, Vector3.zero)));
            Assert.DoesNotThrow(() => audio.OnHitConfirmed(new MeleeHitCue(null, BodyLocation.Torso, 2, Vector3.zero)));
            Assert.AreEqual(0, service.ActiveCount);
        }
    }
}
