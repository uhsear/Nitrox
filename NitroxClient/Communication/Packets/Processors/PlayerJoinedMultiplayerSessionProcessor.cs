using System.Collections;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using UWE;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class PlayerJoinedMultiplayerSessionProcessor(PlayerManager playerManager, Entities entities) : IClientPacketProcessor<PlayerJoinedMultiplayerSession>
{
    private readonly Entities entities = entities;
    private readonly PlayerManager playerManager = playerManager;

    public Task Process(ClientProcessorContext context, PlayerJoinedMultiplayerSession packet)
    {
        // Check if this player is reconnecting within the disconnect grace period.
        Optional<RemotePlayer> existingPlayer = playerManager.Find(packet.PlayerContext.SessionId);
        if (existingPlayer.HasValue && DisconnectProcessor.TryCancelPendingDisconnect(packet.PlayerContext.SessionId, existingPlayer.Value))
        {
            Log.Info($"{packet.PlayerContext.PlayerName} reconnected within grace period");
            return Task.CompletedTask;
        }

        CoroutineHost.StartCoroutine(SpawnRemotePlayer(packet));
        return Task.CompletedTask;
    }

    private IEnumerator SpawnRemotePlayer(PlayerJoinedMultiplayerSession packet)
    {
        playerManager.Create(packet.PlayerContext);
        yield return entities.SpawnEntityAsync(packet.PlayerEntity, true, true);

        Log.Info($"{packet.PlayerContext.PlayerName} joined the game");
        Log.InGame(Language.main.Get("Nitrox_PlayerJoined").Replace("{PLAYER}", packet.PlayerContext.PlayerName));
    }
}
