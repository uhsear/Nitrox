using System;
using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Battery_charge_set_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Property((Battery t) => t.charge).SetMethod;

    public static void Prefix(Battery __instance, float value)
    {
        // Broadcast update once per integer change, and also when the battery reaches
        // full depletion (0) or full charge (capacity). This prevents the discharge state
        // from getting stuck at a random percentage when the value hovers between integer
        // boundaries without crossing one.
        bool crossedIntegerBoundary = Math.Abs(Math.Floor(__instance.charge) - Math.Floor(value)) > 0.0;
        bool reachedEmpty = value <= 0f && __instance.charge > 0f;
        bool reachedFull = value >= __instance.capacity && __instance.charge < __instance.capacity;

        if ((crossedIntegerBoundary || reachedEmpty || reachedFull) &&
            __instance.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<Entities>().EntityMetadataChanged(__instance, id);
        }
    }
}
