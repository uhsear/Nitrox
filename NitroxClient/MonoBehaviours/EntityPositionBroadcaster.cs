using System.Collections.Generic;
using System.Linq;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;
using static Nitrox.Model.Subnautica.Packets.EntityTransformUpdates;

namespace NitroxClient.MonoBehaviours;

public class EntityPositionBroadcaster : MonoBehaviour
{
    public static readonly float BROADCAST_INTERVAL = 0.25f;

    /// <summary>
    /// Minimum position change (in unity units) before an entity update is broadcast.
    /// Stationary entities below this threshold are suppressed to save bandwidth.
    /// </summary>
    private const float POSITION_THRESHOLD = 0.01f;

    /// <summary>
    /// Minimum rotation change (in degrees) before an entity update is broadcast.
    /// </summary>
    private const float ROTATION_THRESHOLD = 0.1f;

    /// <summary>
    /// Maximum time (in seconds) between broadcasts for a stationary entity,
    /// ensuring eventual consistency even when nothing moves.
    /// </summary>
    private const float MAX_TIME_WITHOUT_BROADCAST = 5f;

    private static HashSet<NitroxId> watchingEntityIds = new();

    private static Dictionary<NitroxId, SplineTransformUpdate> splineUpdatesById = new();

    /// <summary>
    /// Tracks last broadcast position per entity for stationary suppression.
    /// </summary>
    private static Dictionary<NitroxId, (Vector3 Position, Quaternion Rotation, float LastBroadcastTime)> lastBroadcastState = new();

    private IPacketSender packetSender;

    private float time;

    public void Awake()
    {
        packetSender = NitroxServiceLocator.LocateService<IPacketSender>();
    }

    public void Update()
    {
        time += Time.deltaTime;

        // Only do on a specific cadence to avoid hammering server
        if (time >= BROADCAST_INTERVAL)
        {
            time = 0;

            if (watchingEntityIds.Count > 0)
            {
                Dictionary<NitroxId, GameObject> nonSplineEntitiesById = NitroxEntity.GetObjectsFrom(watchingEntityIds)
                                                                                     .Where(item => !item.Value.GetComponent<SwimBehaviour>() &&
                                                                                                    !item.Value.GetComponent<WalkBehaviour>())
                                                                                     .ToDictionary(item => item.Key, item => item.Value);

                List<EntityTransformUpdate> updates = BuildUpdates(nonSplineEntitiesById);

                if (updates.Count > 0)
                {
                    packetSender.Send(new EntityTransformUpdates(updates));
                }
            }
        }
    }

    private List<EntityTransformUpdate> BuildUpdates(Dictionary<NitroxId, GameObject> nonSplineEntitiesById)
    {
        List<EntityTransformUpdate> updates = new();
        float currentTime = Time.time;

        foreach (KeyValuePair<NitroxId, GameObject> gameObjectWithId in nonSplineEntitiesById)
        {
            if (gameObjectWithId.Value)
            {
                Vector3 currentPosition = gameObjectWithId.Value.transform.position;
                Quaternion currentRotation = gameObjectWithId.Value.transform.rotation;

                bool shouldBroadcast = true;

                if (lastBroadcastState.TryGetValue(gameObjectWithId.Key, out var lastState))
                {
                    float positionDelta = Vector3.Distance(lastState.Position, currentPosition);
                    float rotationDelta = Quaternion.Angle(lastState.Rotation, currentRotation);
                    float timeSinceLastBroadcast = currentTime - lastState.LastBroadcastTime;

                    // Suppress if position and rotation haven't changed enough, unless we've exceeded the safety interval
                    if (positionDelta <= POSITION_THRESHOLD && rotationDelta <= ROTATION_THRESHOLD && timeSinceLastBroadcast < MAX_TIME_WITHOUT_BROADCAST)
                    {
                        shouldBroadcast = false;
                    }
                }

                if (shouldBroadcast)
                {
                    updates.Add(new RawTransformUpdate(gameObjectWithId.Key, currentPosition.ToDto(), currentRotation.ToDto()));
                    lastBroadcastState[gameObjectWithId.Key] = (currentPosition, currentRotation, currentTime);
                }
            }
        }

        // Only send data for entities still simulated by the local player
        updates.AddRange(splineUpdatesById.Values.Where(
            splineUpdate => this.Resolve<SimulationOwnership>().HasAnyLockType(splineUpdate.Id)
        ));

        splineUpdatesById.Clear();

        return updates;
    }

    public static void WatchEntity(NitroxId id)
    {
        watchingEntityIds.Add(id);

        // The game object may not exist at this very moment (due to being spawned in async). This is OK as we will
        // automatically start sending updates when we finally get it in the world. This behavior will also allow us
        // to resync or respawn entities while still have broadcasting enabled without doing anything extra.
        
        if (NitroxEntity.TryGetComponentFrom(id, out RemotelyControlled remotelyControlled))
        {
            Object.Destroy(remotelyControlled);
        }
    }

    public static void StopWatchingEntity(NitroxId id)
    {
        watchingEntityIds.Remove(id);
        lastBroadcastState.Remove(id);
    }

    public static void RegisterSplineMovementChange(NitroxId id, GameObject gameObject, Vector3 targetPos, Vector3 targetDir, float velocity)
    {
        if (watchingEntityIds.Contains(id))
        {
            splineUpdatesById[id] = new(id, gameObject.transform.position.ToDto(), gameObject.transform.rotation.ToDto(), targetPos.ToDto(), targetDir.ToDto(), velocity);
        }
    }

    public static void RemoveEntityMovementControl(GameObject gameObject, NitroxId entityId)
    {
        if (gameObject.TryGetComponent(out RemotelyControlled remotelyControlled))
        {
            Destroy(remotelyControlled);
        }
        StopWatchingEntity(entityId);
    }
}
