using System;
using System.Threading.Tasks;
using MagicOnion;
using MessagePack;

namespace Nitrox.Model.MagicOnion;

/// <summary>
///     See <a href="https://cysharp.github.io/MagicOnion/streaminghub/getting-started#steps">MagicOnion docs</a>
/// </summary>
public interface IServersManagement : IStreamingHub<IServersManagement, IServerManagementReceiver>
{
    ValueTask SetPlayers(string[] players);
    ValueTask AddOutputLine(string category, DateTimeOffset? localTime, int level, string message);
    ValueTask SetServerStatus(ServerStatusInfo status);
}

/// <summary>
///     The client-side interface. However in this case, the launcher project is the gRPC server and the game server is the
///     gRPC client.
/// </summary>
public interface IServerManagementReceiver
{
    void OnCommand(string command);
}

/// <summary>
///     Lightweight snapshot of current server state, pushed to the launcher for display/monitoring.
/// </summary>
[MessagePackObject]
public class ServerStatusInfo
{
    [Key(0)]
    public int PlayerCount { get; set; }

    [Key(1)]
    public int MaxPlayers { get; set; }

    [Key(2)]
    public double UptimeSeconds { get; set; }

    [Key(3)]
    public string Version { get; set; } = "";

    [Key(4)]
    public string SaveName { get; set; } = "";

    [Key(5)]
    public DateTimeOffset LastSaveTime { get; set; }

    [Key(6)]
    public bool IsAutoSaveEnabled { get; set; }

    [Key(7)]
    public int ServerPort { get; set; }

    [Key(8)]
    public string GameMode { get; set; } = "";
}
