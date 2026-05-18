using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours.Cyclops;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

public class PlayerMovementBroadcaster : MonoBehaviour
{
    /// <summary>
    /// Normal broadcast interval when the player is moving (30 Hz).
    /// </summary>
    private const float ACTIVE_BROADCAST_PERIOD = 1f / 30f;

    /// <summary>
    /// Reduced broadcast interval when the player is stationary (1 Hz) to save bandwidth.
    /// </summary>
    private const float STATIONARY_BROADCAST_PERIOD = 1f;

    /// <summary>
    /// Minimum position change (in unity units) to consider the player as moving.
    /// </summary>
    private const float POSITION_THRESHOLD = 0.01f;

    /// <summary>
    /// Minimum rotation change (in degrees) to consider the player as having rotated.
    /// </summary>
    private const float ROTATION_THRESHOLD = 0.1f;

    private LocalPlayer localPlayer;
    private float lastBroadcastTime;
    private Vector3 lastBroadcastPosition;
    private Quaternion lastBroadcastBodyRotation;
    private Quaternion lastBroadcastAimingRotation;

    public void Awake()
    {
        localPlayer = this.Resolve<LocalPlayer>();
    }

    public void Update()
    {
        // TODO: Replace this temporary fix. Mostly prevents server console being spammed with warnings when a client is in the queue.
        // There should be a way to block all packets from being sent when in the join queue or during initial sync.
        if (!Multiplayer.Main.InitialSyncCompleted)
        {
            return;
        }

        // Freecam does disable main camera control
        // But it's also disabled when driving the cyclops through a cyclops camera (content.activeSelf is only true when controlling through a cyclops camera)
        if (!MainCameraControl.main.isActiveAndEnabled &&
            !uGUI_CameraCyclops.main.content.activeSelf)
        {
            return;
        }

        if (BroadcastPlayerInCyclopsMovement())
        {
            return;
        }

        if (Player.main.isPiloting)
        {
            return;
        }

        Vector3 currentPosition = Player.main.transform.position;
        Vector3 playerVelocity = Player.main.playerController.velocity;

        // IDEA: possibly only CameraRotation is of interest, because bodyrotation is extracted from that.
        Quaternion bodyRotation = MainCameraControl.main.viewModel.transform.rotation;
        Quaternion aimingRotation = Player.main.camRoot.GetAimingTransform().rotation;

        SubRoot subRoot = Player.main.GetCurrentSub();

        // If in a subroot the position will be relative to the subroot
        if (subRoot)
        {
            // Rotate relative player position relative to the subroot (else there are problems with respawning)
            Transform subRootTransform = subRoot.transform;
            Quaternion undoVehicleAngle = subRootTransform.rotation.GetInverse();
            currentPosition = currentPosition - subRootTransform.position;
            currentPosition = undoVehicleAngle * currentPosition;
            bodyRotation = undoVehicleAngle * bodyRotation;
            aimingRotation = undoVehicleAngle * aimingRotation;
            currentPosition = subRootTransform.TransformPoint(currentPosition);
        }

        // Determine if the player has moved or rotated significantly since the last broadcast
        float positionDelta = Vector3.Distance(lastBroadcastPosition, currentPosition);
        float bodyRotationDelta = Quaternion.Angle(lastBroadcastBodyRotation, bodyRotation);
        float aimingRotationDelta = Quaternion.Angle(lastBroadcastAimingRotation, aimingRotation);

        bool hasMoved = positionDelta > POSITION_THRESHOLD ||
                        bodyRotationDelta > ROTATION_THRESHOLD ||
                        aimingRotationDelta > ROTATION_THRESHOLD;

        // Use faster broadcast rate when moving, slower when stationary
        float broadcastPeriod = hasMoved ? ACTIVE_BROADCAST_PERIOD : STATIONARY_BROADCAST_PERIOD;
        float currentTime = Time.time;

        if (currentTime < lastBroadcastTime + broadcastPeriod)
        {
            return;
        }

        lastBroadcastTime = currentTime;
        lastBroadcastPosition = currentPosition;
        lastBroadcastBodyRotation = bodyRotation;
        lastBroadcastAimingRotation = aimingRotation;

        localPlayer.BroadcastLocation(currentPosition, playerVelocity, bodyRotation, aimingRotation);
    }

    private bool BroadcastPlayerInCyclopsMovement()
    {
        if (!Player.main.isPiloting && Player.main.TryGetComponent(out CyclopsMotor cyclopsMotor) && cyclopsMotor.Pawn != null)
        {
            Transform pawnTransform = cyclopsMotor.Pawn.Handle.transform;
            PlayerInCyclopsMovement packet = new(this.Resolve<LocalPlayer>().SessionId.Value, pawnTransform.localPosition.ToDto(), pawnTransform.localRotation.ToDto());
            this.Resolve<IPacketSender>().Send(packet);
            return true;
        }
        return false;
    }
}
