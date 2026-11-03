using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleRPG.Audio;
using SimpleRPG.InventorySystem;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// The inventory knows nothing of sound, so nothing but the runtime hook in <see cref="InventoryMoveSounds"/> makes a move audible. If that
    /// subscription is lost, moves go silent without any error; this fails instead.
    /// </summary>
    public sealed class InventoryMoveSoundWiringPlayModeTests
    {
        [Test]
        public void InPlayMode_TheMoveSoundsObserveLocalItemMoves()
        {
            FieldInfo backing = typeof(Inventory).GetField("LocalItemMoved", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(backing, "Inventory.LocalItemMoved is expected to be a field-like event");

            var handlers = (Delegate)backing.GetValue(null);

            Assert.IsNotNull(handlers, "nothing observes local item moves, so moving an item makes no sound");
            Assert.AreEqual(1, handlers.GetInvocationList().Count(handler => handler.Method.DeclaringType == typeof(InventoryMoveSounds)),
                "InventoryMoveSounds should be subscribed exactly once");
        }
    }
}
