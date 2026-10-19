using System.Collections.Generic;
using System.Linq;
using LevelGeneration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the fingerprints used to detect a client that would build a different dungeon, and the random streams behind seed stability.
    /// </summary>
    public class LayoutFingerprintTests
    {
        private const string ModularDungeonPath = "Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset";

        [Test]
        public void SameRoomset_GivesTheSameFingerprint()
        {
            Roomset roomset = AssetDatabase.LoadAssetAtPath<Roomset>(ModularDungeonPath);

            Assert.AreEqual(
                LayoutFingerprint.OfRoomset(RoomDefinitionBaker.Bake(roomset)),
                LayoutFingerprint.OfRoomset(RoomDefinitionBaker.Bake(roomset)));
        }

        [Test]
        public void ChangingADoor_ChangesTheRoomsetFingerprint()
        {
            List<RoomDefinition> original = TestRooms.MixedSet();
            int before = LayoutFingerprint.OfRoomset(original);

            RoomDefinition corridor = original[1];
            var longer = new List<DoorDefinition>(corridor.Doors);
            longer[0] = new DoorDefinition(0, longer[0].Point, longer[0].Side, longer[0].LengthCells + 1);
            var changed = new List<RoomDefinition>(original) { [1] = new RoomDefinition(1, corridor.Name, corridor.Size, corridor.OriginOffset, longer) };

            Assert.AreNotEqual(before, LayoutFingerprint.OfRoomset(changed));
        }

        [Test]
        public void ChangingRoomOrderOrPolicy_ChangesTheRoomsetFingerprint()
        {
            List<RoomDefinition> original = TestRooms.MixedSet();
            int before = LayoutFingerprint.OfRoomset(original);

            Assert.AreNotEqual(before, LayoutFingerprint.OfRoomset(Enumerable.Reverse(original).ToList()), "Order decides which room a seed picks.");

            RoomDefinition heavier = new RoomDefinition(0, original[0].Name, original[0].Size, original[0].OriginOffset, original[0].Doors, weight: 5f);
            var reweighted = new List<RoomDefinition>(original) { [0] = heavier };
            Assert.AreNotEqual(before, LayoutFingerprint.OfRoomset(reweighted), "Weights decide which room a seed picks.");
        }

        [Test]
        public void Layouts_AreChecksummedByWhereTheirRoomsStand()
        {
            List<RoomDefinition> set = TestRooms.MixedSet();

            int first = LayoutFingerprint.OfLayout(new LayoutGenerator(1, set).Generate(10).Layout);
            int again = LayoutFingerprint.OfLayout(new LayoutGenerator(1, set).Generate(10).Layout);
            var others = Enumerable.Range(2, 8).Select(seed => LayoutFingerprint.OfLayout(new LayoutGenerator(seed, set).Generate(10).Layout));

            Assert.AreEqual(first, again);
            Assert.That(others, Has.Some.Not.EqualTo(first), "Different seeds should not all give the same layout.");
        }

        [Test]
        public void Fingerprints_AreNeverZero()
        {
            List<RoomDefinition> set = TestRooms.MixedSet();

            Assert.AreNotEqual(0, LayoutFingerprint.OfRoomset(set));
            Assert.AreNotEqual(0, LayoutFingerprint.OfLayout(new LayoutGenerator(3, set).Generate(6).Layout));
        }

        [Test]
        public void Check_AcceptsMatchesAndMissingValues_AndDescribesMismatches()
        {
            Assert.IsNull(LayoutFingerprint.Check(0, 1234), "Zero means the server sent no fingerprint.");
            Assert.IsNull(LayoutFingerprint.Check(1234, 1234));
            StringAssert.Contains("000004D2", LayoutFingerprint.Check(1234, 99));
        }

        [Test]
        public void RandomStreams_AreIndependentOfEachOtherAndStable()
        {
            int[] streams = Enumerable.Range(1, 3).Select(stream => LayoutGenerator.StreamSeed(42, stream)).ToArray();

            Assert.That(streams.Distinct().Count(), Is.EqualTo(3), "Each stream needs its own seed.");
            Assert.AreEqual(streams, Enumerable.Range(1, 3).Select(stream => LayoutGenerator.StreamSeed(42, stream)).ToArray());
            Assert.AreNotEqual(LayoutGenerator.StreamSeed(42, 1), LayoutGenerator.StreamSeed(43, 1));
        }
    }
}
