using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class LiveMixin_Kill_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((LiveMixin t) => t.Kill(default));

    public static void Postfix(LiveMixin __instance)
    {
        if (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted)
        {
            return;
        }

        // We don't broadcast if we don't have objectId or if the object is whitelisted,
        // in which case kill broadcast is managed differently
        if (!__instance.TryGetNitroxId(out NitroxId objectId) ||
            Resolve<LiveMixinManager>().IsWhitelistedUpdateType(__instance))
        {
            return;
        }

        // For creatures with CreatureDeath where the killing player is NOT the simulation owner,
        // broadcast a RemoveCreatureCorpse packet so that the server and all connected clients
        // see the creature die. Without this, a non-simulating player kills a creature locally
        // but other players see it still alive and fleeing (Issue #2510).
        // When the killing player IS the simulation owner, OnKillAsync's transpiler already handles broadcasting.
        if (__instance.TryGetComponent(out CreatureDeath creatureDeath) &&
            !Resolve<LiveMixinManager>().IsRemoteHealthChanging &&
            !Resolve<SimulationOwnership>().HasAnyLockType(objectId))
        {
            Resolve<IPacketSender>().Send(new RemoveCreatureCorpse(objectId, creatureDeath.transform.localPosition.ToDto(), creatureDeath.transform.localRotation.ToDto(), creatureDeath.lastDamageWasHeat));
            return;
        }

        // Some objects don't have destroyOnDeath but we still need to broadcast the death
        // (because the destruction is managed by another script)
        if (__instance.destroyOnDeath || Resolve<LiveMixinManager>().ShouldBroadcastDeath(__instance))
        {
            Resolve<IPacketSender>().Send(new EntityDestroyed(objectId));
        }
    }
}
