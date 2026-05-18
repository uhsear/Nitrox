using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class RemoveCreatureCorpse : Packet
{
    public NitroxId CreatureId { get; }
    public NitroxVector3 DeathPosition { get; }
    public NitroxQuaternion DeathRotation { get; }

    /// <summary>
    /// Whether the killing blow was heat damage (e.g. Thermoblade/HeatBlade).
    /// When true, the creature should drop a cooked item instead of a raw corpse.
    /// </summary>
    public bool LastDamageWasHeat { get; }

    public RemoveCreatureCorpse(NitroxId creatureId, NitroxVector3 deathPosition, NitroxQuaternion deathRotation, bool lastDamageWasHeat = false)
    {
        CreatureId = creatureId;
        DeathPosition = deathPosition;
        DeathRotation = deathRotation;
        LastDamageWasHeat = lastDamageWasHeat;
    }
}
