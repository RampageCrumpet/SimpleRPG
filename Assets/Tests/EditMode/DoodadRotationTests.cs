using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Tests
{
    public class DoodadRotationTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        private Doodad Make(bool x, bool y, bool z, float increment)
        {
            host = new GameObject("doodad");
            var doodad = host.AddComponent<Doodad>();
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Doodad).GetField("rotationIncrement", Flags).SetValue(doodad, increment);
            typeof(Doodad).GetField("rotateXAxis", Flags).SetValue(doodad, x);
            typeof(Doodad).GetField("rotateYAxis", Flags).SetValue(doodad, y);
            typeof(Doodad).GetField("rotateZAxis", Flags).SetValue(doodad, z);
            typeof(Doodad).GetMethod("Start", Flags).Invoke(doodad, null);
            return doodad;
        }

        [Test]
        public void NoAxisEnabled_LeavesAValidIdentityRotation()
        {
            Make(false, false, false, 45f);
            Quaternion rotation = host.transform.rotation;
            Assert.AreEqual(1f, rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w, 1e-5f, "a rotation must be a unit quaternion");
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, rotation), 1e-3f);
        }

        [Test]
        public void EnabledAxes_GiveUnitRotationsInWholeIncrements()
        {
            for (int i = 0; i < 50; i++)
            {
                Object.DestroyImmediate(host);
                Make(false, true, false, 45f);
                Quaternion rotation = host.transform.rotation;
                Assert.AreEqual(1f, rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w, 1e-5f);
                float y = Mathf.Repeat(host.transform.eulerAngles.y, 45f);
                Assert.IsTrue(y < 0.01f || y > 44.99f, $"yaw {host.transform.eulerAngles.y} is not a multiple of 45");
                Assert.AreEqual(0f, Mathf.DeltaAngle(0f, host.transform.eulerAngles.x), 0.01f);
            }
        }
    }
}
