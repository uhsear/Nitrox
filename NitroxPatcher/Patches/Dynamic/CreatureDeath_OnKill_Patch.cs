using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Prevents <see cref="CreatureDeath.OnKill"/> from happening on non-simulated entities,
/// unless the creature's health has reached zero (confirmed dead). This ensures that
/// when a non-simulating player deals lethal damage, the creature still dies locally
/// and the death is broadcast to all players (Issue #2510).
/// </summary>
public sealed partial class CreatureDeath_OnKill_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CreatureDeath t) => t.OnKill());

    public static bool Prefix(CreatureDeath __instance)
    {
        if (__instance.TryGetNitroxId(out NitroxId creatureId) &&
            Resolve<SimulationOwnership>().HasAnyLockType(creatureId))
        {
            return true;
        }

        // Allow the kill to proceed if the creature's health has actually reached zero,
        // even without simulation ownership. The death broadcast is handled by LiveMixin_Kill_Patch.
        if (__instance.liveMixin && __instance.liveMixin.health <= 0f)
        {
            return true;
        }

        return false;
    }
}
