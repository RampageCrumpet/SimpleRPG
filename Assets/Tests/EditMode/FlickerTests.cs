using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Tests
{
    public class FlickerTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        private Flicker Make(Vector3 position, float intensity = 2f, float range = 8f)
        {
            host = new GameObject("torch");
            host.transform.position = position;
            Light light = host.AddComponent<Light>();
            light.intensity = intensity;
            light.range = range;
            Flicker flicker = host.AddComponent<Flicker>();
            typeof(Flicker).GetMethod("OnEnable", Flags).Invoke(flicker, null);
            return flicker;
        }

        [Test]
        public void Sample_StaysInsideTheUnitRange()
        {
            for (int i = 0; i < 2000; i++)
            {
                float value = Flicker.Sample(i * 0.037f, i % 50, 1.2f, 11f, 0.35f);
                Assert.That(value, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Sample_IsTheSameForTheSameInputs()
        {
            Assert.AreEqual(Flicker.Sample(3.3f, 7f, 1.2f, 11f, 0.35f), Flicker.Sample(3.3f, 7f, 1.2f, 11f, 0.35f));
        }

        [Test]
        public void Sample_ActuallyFlickers()
        {
            float min = 1f, max = 0f;
            for (int i = 0; i < 2000; i++)
            {
                float value = Flicker.Sample(i * 0.05f, 4f, 1.2f, 11f, 0.35f);
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }
            Assert.That(max - min, Is.GreaterThan(0.5f), "a flame should use most of its range over a few seconds");
        }

        [Test]
        public void DifferentPositions_GetDifferentSeeds()
        {
            Assert.AreNotEqual(Flicker.SeedFromPosition(new Vector3(4f, 2f, 8f)), Flicker.SeedFromPosition(new Vector3(12f, 2f, 8f)));
        }

        [Test]
        public void Apply_KeepsIntensityAndRangeInsideTheConfiguredBand()
        {
            Flicker flicker = Make(new Vector3(3f, 1f, 5f));
            Light light = host.GetComponent<Light>();
            for (int i = 0; i < 500; i++)
            {
                flicker.Apply(i * 0.1f, 0.1f);
                Assert.That(light.intensity, Is.InRange(2f * 0.85f - 1e-4f, 2f * 1.08f + 1e-4f));
                Assert.That(light.range, Is.InRange(8f * 0.94f - 1e-4f, 8f * 1.06f + 1e-4f));
            }
        }

        [Test]
        public void Smoothing_LimitsHowFastTheLightCanChangeBetweenFrames()
        {
            Flicker flicker = Make(new Vector3(3f, 1f, 5f));
            Light light = host.GetComponent<Light>();
            flicker.Apply(0f, 0.016f);
            float previous = light.intensity;
            float largestStep = 0f;
            for (int i = 1; i < 1000; i++)
            {
                flicker.Apply(i * 0.016f, 0.016f);
                largestStep = Mathf.Max(largestStep, Mathf.Abs(light.intensity - previous));
                previous = light.intensity;
            }
            Assert.That(largestStep, Is.LessThan(0.06f), "no frame should visibly pop");
        }

        [Test]
        public void Extinguish_FadesDownThenSwitchesTheLightOff()
        {
            Flicker flicker = Make(new Vector3(3f, 1f, 5f));
            Light light = host.GetComponent<Light>();
            flicker.Apply(0f, 0.016f);
            flicker.Extinguish();
            Assert.IsFalse(flicker.IsLit);

            flicker.Apply(0.1f, 0.1f);
            Assert.IsTrue(light.enabled, "it should still be burning part way through the fade");
            Assert.IsFalse(flicker.IsDark);

            for (int i = 0; i < 20; i++) flicker.Apply(0.2f + i * 0.1f, 0.1f);
            Assert.IsFalse(light.enabled);
            Assert.IsTrue(flicker.IsDark);
        }

        [Test]
        public void Ignite_FadesBackUp()
        {
            Flicker flicker = Make(new Vector3(3f, 1f, 5f));
            Light light = host.GetComponent<Light>();
            flicker.Extinguish();
            for (int i = 0; i < 20; i++) flicker.Apply(i * 0.1f, 0.1f);
            Assert.IsTrue(flicker.IsDark);

            flicker.Ignite();
            flicker.Apply(2.1f, 0.1f);
            Assert.IsTrue(light.enabled);
            Assert.That(light.intensity, Is.GreaterThan(0f).And.LessThan(2f * 0.85f), "still fading up");

            for (int i = 0; i < 20; i++) flicker.Apply(2.2f + i * 0.1f, 0.1f);
            Assert.That(light.intensity, Is.InRange(2f * 0.85f - 1e-4f, 2f * 1.08f + 1e-4f));
        }

        [Test]
        public void StartUnlit_BeginsDark()
        {
            host = new GameObject("torch");
            Light light = host.AddComponent<Light>();
            Flicker flicker = host.AddComponent<Flicker>();
            typeof(Flicker).GetField("startLit", Flags).SetValue(flicker, false);
            typeof(Flicker).GetMethod("OnEnable", Flags).Invoke(flicker, null);
            Assert.IsFalse(light.enabled);
            Assert.IsTrue(flicker.IsDark);
        }

        [Test]
        public void Apply_KeepsTheLightNearItsMount()
        {
            Flicker flicker = Make(new Vector3(3f, 1f, 5f));
            for (int i = 0; i < 500; i++)
            {
                flicker.Apply(i * 0.1f, 0.1f);
                Assert.That(Vector3.Distance(host.transform.position, new Vector3(3f, 1f, 5f)), Is.LessThan(0.06f));
            }
        }

        [Test]
        public void Disabling_PutsTheLightBackAsItWasAuthored()
        {
            Flicker flicker = Make(new Vector3(3f, 1f, 5f));
            Light light = host.GetComponent<Light>();
            Color authored = light.color;
            flicker.Apply(1.7f, 0.016f);
            typeof(Flicker).GetMethod("OnDisable", Flags).Invoke(flicker, null);
            Assert.AreEqual(2f, light.intensity, 1e-5f);
            Assert.AreEqual(8f, light.range, 1e-5f);
            Assert.AreEqual(authored, light.color);
            Assert.AreEqual(new Vector3(3f, 1f, 5f), host.transform.position);
        }
    }
}
