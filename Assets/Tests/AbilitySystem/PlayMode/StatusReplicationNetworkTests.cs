using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Gameplay;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// A status the server applies must show on a genuinely separate client: appearing, stacking, refreshing and ending, including for a
    /// client that joined after the status began.
    /// </summary>
    public class StatusReplicationNetworkTests : RemoteClientTestBase
    {
        private DefinitionRegistry previousDefinitions;
        private StatusDefinition burning;

        [UnitySetUp]
        public IEnumerator RegisterStatus()
        {
            burning = ScriptableObject.CreateInstance<StatusDefinition>();
            burning.name = "Burning";
            burning.AssignNewId();
            burning.Configure("Burning", 5f, 0f);
            created.Add(burning);

            previousDefinitions = DefinitionRegistry.Instance;
            DefinitionRegistry registry = ScriptableObject.CreateInstance<DefinitionRegistry>();
            created.Add(registry);
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { burning }).Success);
            DefinitionRegistry.Instance = registry;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreRegistry()
        {
            DefinitionRegistry.Instance = previousDefinitions;
            yield return null;
        }

        private static StatusManager Statuses => AbilityServices.Current.Statuses;

        private StatusApplyResult Apply(Character target, double potency = 1) =>
            Statuses.Apply(new StatusApplication(target.Id, burning, SourceHandle.FromEntity(target.Id), target.Id, potency));

        private static List<BuffEntry> Buffs(Character character)
        {
            List<BuffEntry> buffs = new List<BuffEntry>();
            character.GetComponent<AbilityUser>().GetBuffs(buffs);
            return buffs;
        }

        [UnityTest]
        public IEnumerator AStatusTheServerApplies_AppearsOnTheClient_StacksAndEnds()
        {
            Character victim = SpawnOnServer(TargetPrefab, Vector3.zero);
            Character clientVictim = null;
            yield return ClientCopyOf(victim, c => clientVictim = c);
            Assert.IsEmpty(Buffs(clientVictim));

            Assert.AreEqual(StatusApplyResult.Started, Apply(victim));
            yield return Until(() => Buffs(clientVictim).Count == 1, 5f, "the client to see the status");
            Assert.AreEqual("Burning", Buffs(clientVictim)[0].Name);
            Assert.AreEqual(1, Buffs(clientVictim)[0].Stacks);

            Apply(victim);
            yield return Until(() => Buffs(clientVictim).Single().Stacks == 2, 5f, "the client to see the second stack");

            Statuses.Dispel(victim.Id, burning);
            yield return Until(() => Buffs(clientVictim).Count == 0, 5f, "the client to see the status end");
        }

        [UnityTest]
        public IEnumerator ARefresh_ReachesTheClient_AsALaterEndingTick()
        {
            StatusDefinition refreshing = ScriptableObject.CreateInstance<StatusDefinition>();
            refreshing.name = "Chilled";
            refreshing.AssignNewId();
            refreshing.Configure("Chilled", 5f, 0f, reapplication: ReapplicationPolicy.RefreshOnly);
            created.Add(refreshing);
            Assert.IsTrue(DefinitionRegistry.Instance.Rebuild(new GameplayDefinition[] { burning, refreshing }).Success);

            Character victim = SpawnOnServer(TargetPrefab, Vector3.zero);
            Character clientVictim = null;
            yield return ClientCopyOf(victim, c => clientVictim = c);

            Statuses.Apply(new StatusApplication(victim.Id, refreshing, SourceHandle.FromEntity(victim.Id), victim.Id, 1));
            yield return Until(() => clientVictim.GetComponent<AbilityUser>().GetActiveStatuses().Count == 1, 5f, "the client to see the status");
            long firstEnd = clientVictim.GetComponent<AbilityUser>().GetActiveStatuses()[0].ExpiryTick;

            yield return Wait(0.5f);
            Assert.AreEqual(StatusApplyResult.Refreshed, Statuses.Apply(new StatusApplication(victim.Id, refreshing, SourceHandle.FromEntity(victim.Id), victim.Id, 1)));
            yield return Until(() => clientVictim.GetComponent<AbilityUser>().GetActiveStatuses()[0].ExpiryTick > firstEnd, 5f, "the client to see the refreshed end");
            Assert.AreEqual(1, Buffs(clientVictim).Count, "A refresh adds no second entry.");
        }

        [UnityTest]
        public IEnumerator ADeadCharactersStatuses_DisappearFromTheClient()
        {
            Character victim = SpawnOnServer(TargetPrefab, Vector3.zero);
            Character clientVictim = null;
            yield return ClientCopyOf(victim, c => clientVictim = c);
            Apply(victim);
            yield return Until(() => Buffs(clientVictim).Count == 1, 5f, "the client to see the status");

            victim.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, HitDetection.BodyLocation.Torso);

            yield return Until(() => Buffs(clientVictim).Count == 0, 5f, "the status to go with the character's life");
        }
    }
}
