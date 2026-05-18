using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using UWE;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class RemoveCreatureCorpseProcessor(Entities entities, LiveMixinManager liveMixinManager, SimulationOwnership simulationOwnership) : IClientPacketProcessor<RemoveCreatureCorpse>
{
    private readonly Entities entities = entities;
    private readonly LiveMixinManager liveMixinManager = liveMixinManager;
    private readonly SimulationOwnership simulationOwnership = simulationOwnership;

    /// <summary>
    ///     Calls only some parts from <see cref="CreatureDeath.OnKillAsync" /> to avoid sending packets from it
    ///     or already synced behaviour (like spawning another respawner from the remote clients)
    /// </summary>
    public static void SafeOnKillAsync(CreatureDeath creatureDeath, NitroxId creatureId, SimulationOwnership simulationOwnership, LiveMixinManager liveMixinManager, bool lastDamageWasHeat = false)
    {
        // Ensure we don't broadcast anything from this kill event
        simulationOwnership.StopSimulatingEntity(creatureId);

        // Remove the position broadcasting stuff from it
        EntityPositionBroadcaster.RemoveEntityMovementControl(creatureDeath.gameObject, creatureId);

        // To avoid SpawnRespawner to be called
        creatureDeath.respawn = false;
        creatureDeath.hasSpawnedRespawner = true;

        // Propagate the heat damage flag so the creature drops cooked food when killed by a Thermoblade.
        // Previously this was hardcoded to false, causing non-simulating players' HeatBlade kills to
        // always drop raw fish instead of cooked.
        creatureDeath.lastDamageWasHeat = lastDamageWasHeat;

        // Receiving this packet means the creature is dead
        LiveMixin liveMixin = creatureDeath.liveMixin;
        liveMixin.health = 0f;
        liveMixin.tempDamage = 0f;
        // We don't care what's inside the damage info
        liveMixin.damageInfo.Clear();
        liveMixin.NotifyAllAttachedDamageReceivers(liveMixin.damageInfo);

        using (PacketSuppressor<EntitySpawnedByClient>.Suppress())
        using (PacketSuppressor<RemoveCreatureCorpse>.Suppress())
        {
            CoroutineUtils.PumpCoroutine(creatureDeath.OnKillAsync());
        }
    }

    public Task Process(ClientProcessorContext context, RemoveCreatureCorpse packet)
    {
        entities.RemoveEntity(packet.CreatureId);

        if (entities.SpawningEntities)
        {
            entities.MarkForDeletion(packet.CreatureId);
        }

        if (!NitroxEntity.TryGetComponentFrom(packet.CreatureId, out CreatureDeath creatureDeath))
        {
            Log.Warn($"[{nameof(RemoveCreatureCorpseProcessor)}] Could not find entity with id: {packet.CreatureId} to remove corpse from.");
            return Task.CompletedTask;
        }

        creatureDeath.transform.localPosition = packet.DeathPosition.ToUnity();
        creatureDeath.transform.localRotation = packet.DeathRotation.ToUnity();

        SafeOnKillAsync(creatureDeath, packet.CreatureId, simulationOwnership, liveMixinManager, packet.LastDamageWasHeat);
        return Task.CompletedTask;
    }
}
