using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.HUD;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class DisconnectProcessor(PlayerManager remotePlayerManager, PlayerVitalsManager vitalsManager) : IClientPacketProcessor<Disconnect>
{
    /// <summary>
    ///     Grace period in seconds before a disconnected player is fully removed.
    ///     If the player reconnects within this window, their visual state is restored.
    /// </summary>
    private const float DISCONNECT_GRACE_PERIOD_SECONDS = 30f;

    private readonly PlayerManager remotePlayerManager = remotePlayerManager;
    private readonly PlayerVitalsManager vitalsManager = vitalsManager;

    /// <summary>
    ///     Tracks players currently in the disconnect grace period, keyed by session id.
    /// </summary>
    private static readonly Dictionary<SessionId, Coroutine> pendingDisconnects = new();

    public Task Process(ClientProcessorContext context, Disconnect disconnect)
    {
        Optional<RemotePlayer> remotePlayer = remotePlayerManager.Find(disconnect.SessionId);
        if (!remotePlayer.HasValue)
        {
            return Task.CompletedTask;
        }

        RemotePlayer player = remotePlayer.Value;

        // If there is already a pending disconnect for this session (e.g. duplicate packet), ignore it.
        if (pendingDisconnects.ContainsKey(disconnect.SessionId))
        {
            return Task.CompletedTask;
        }

        Log.Info($"{player.PlayerName} disconnected, entering {DISCONNECT_GRACE_PERIOD_SECONDS}s grace period");
        Log.InGame(Language.main.Get("Nitrox_PlayerDisconnected").Replace("{PLAYER}", player.PlayerName));

        // Grey out the player model to visually indicate they are disconnecting.
        SetPlayerGreyedOut(player, true);

        // Start a coroutine that will fully remove the player after the grace period.
        if (player.Body != null)
        {
            MonoBehaviour runner = player.Body.GetComponent<MonoBehaviour>();
            if (runner != null)
            {
                Coroutine gracePeriodCoroutine = runner.StartCoroutine(
                    GracePeriodRemoval(disconnect.SessionId, player));
                pendingDisconnects[disconnect.SessionId] = gracePeriodCoroutine;
            }
            else
            {
                // Fallback: if we cannot start a coroutine, remove immediately.
                FullyRemovePlayer(disconnect.SessionId, player);
            }
        }
        else
        {
            FullyRemovePlayer(disconnect.SessionId, player);
        }

        return Task.CompletedTask;
    }

    private System.Collections.IEnumerator GracePeriodRemoval(SessionId sessionId, RemotePlayer player)
    {
        yield return new WaitForSeconds(DISCONNECT_GRACE_PERIOD_SECONDS);

        // After the grace period, if this session is still pending removal, fully clean up.
        if (pendingDisconnects.Remove(sessionId))
        {
            Log.Info($"{player.PlayerName} grace period expired, fully removing player");
            FullyRemovePlayer(sessionId, player);
        }
    }

    private void FullyRemovePlayer(SessionId sessionId, RemotePlayer player)
    {
        player.PlayerDisconnectEvent.Trigger(player);
        vitalsManager.RemoveForPlayer(sessionId);
        remotePlayerManager.RemovePlayer(sessionId);
    }

    /// <summary>
    ///     If a player reconnects while in the grace period, call this to cancel the pending removal
    ///     and restore their visual state.
    /// </summary>
    public static bool TryCancelPendingDisconnect(SessionId sessionId, RemotePlayer player)
    {
        if (!pendingDisconnects.TryGetValue(sessionId, out Coroutine coroutine))
        {
            return false;
        }

        pendingDisconnects.Remove(sessionId);

        // Stop the grace period coroutine so the player is not removed.
        if (player.Body != null && coroutine != null)
        {
            MonoBehaviour runner = player.Body.GetComponent<MonoBehaviour>();
            if (runner != null)
            {
                runner.StopCoroutine(coroutine);
            }
        }

        // Restore the player's visual appearance.
        SetPlayerGreyedOut(player, false);

        Log.Info($"{player.PlayerName} reconnected within grace period, restoring state");
        Log.InGame(Language.main.Get("Nitrox_PlayerDisconnected").Replace("{PLAYER}", player.PlayerName)
                   .Replace("disconnected", "reconnected"));

        return true;
    }

    /// <summary>
    ///     Toggles a greyed-out visual effect on the remote player to indicate disconnection state.
    /// </summary>
    private static void SetPlayerGreyedOut(RemotePlayer player, bool greyedOut)
    {
        if (player.Body == null)
        {
            return;
        }

        float alpha = greyedOut ? 0.4f : 1f;
        Color tint = greyedOut ? new Color(0.5f, 0.5f, 0.5f, alpha) : Color.white;

        foreach (Renderer renderer in player.Body.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.materials)
            {
                if (material.HasProperty("_Color"))
                {
                    material.color = tint;
                }
            }
        }
    }
}
