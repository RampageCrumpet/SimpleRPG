using NUnit.Framework;
using SimpleRPG.UI;

namespace SimpleRPG.Tests.EditMode
{
    public class HealthBarUITests
    {
        [Test]
        public void Damage_DropsFillAndKeepsTrailDuringDelay()
        {
            float fill = 1f, trail = 1f, timer = 0f;

            HealthBarUI.Step(ref fill, ref trail, ref timer, 0.5f, 0.1f, 2f, 0.35f, 0.4f);

            Assert.Less(fill, 1f);
            Assert.AreEqual(1f, trail, 1e-5f);
            Assert.Greater(timer, 0f);
        }

        [Test]
        public void Trail_CatchesUpToFillAfterDelay()
        {
            float fill = 0.5f, trail = 1f, timer = 0f;

            for (int i = 0; i < 400; i++)
            {
                HealthBarUI.Step(ref fill, ref trail, ref timer, 0.5f, 0.02f, 2f, 0.35f, 0.4f);
            }

            Assert.AreEqual(0.5f, fill, 1e-5f);
            Assert.AreEqual(0.5f, trail, 1e-5f);
        }

        [Test]
        public void Healing_RaisesFillAndTrailNeverFallsBelowIt()
        {
            float fill = 0.2f, trail = 0.2f, timer = 0f;

            HealthBarUI.Step(ref fill, ref trail, ref timer, 0.8f, 0.1f, 2f, 0.35f, 0.4f);

            Assert.Greater(fill, 0.2f);
            Assert.GreaterOrEqual(trail, fill);
        }
    }
}
