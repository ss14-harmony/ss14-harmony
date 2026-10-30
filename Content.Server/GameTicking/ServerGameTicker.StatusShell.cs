using System.Linq;
using System.Text.Json.Nodes;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server.ServerStatus;
using Content.Shared._Harmony.JoinQueue; // Harmony Queue

namespace Content.Server.GameTicking;

public sealed partial class ServerGameTicker
{
    /// <summary>
    ///     Used for thread safety, given <see cref="IStatusHost.OnStatusRequest"/> is called from another thread.
    /// </summary>
    private readonly object _statusShellLock = new();

    /// <summary>
    ///     Round start time in UTC, for status shell purposes.
    /// </summary>
    [ViewVariables]
    private DateTime _roundStartDateTime;

    [Dependency] private IJoinQueueManager _joinQueue = default!; // Harmony Queue

    private void InitializeStatusShell()
    {
        IoCManager.Resolve<IStatusHost>().OnStatusRequest += GetStatusResponse;
    }

    private void GetStatusResponse(JsonNode jObject)
    {
        var preset = CurrentPreset ?? Preset;
        // Harmony start
        var playerCountAdminAdjustment = _cfg.GetCVar(CCVars.AdminsCountInReportedPlayerCount)
            ? 0
            : _adminManager.ActiveAdmins.Count();
        var playerCount = _joinQueue.ActualPlayersCount - playerCountAdminAdjustment;
        // Harmony end

        // This method is raised from another thread, so this better be thread safe!
        lock (_statusShellLock)
        {
            jObject["name"] = _baseServer.ServerName;
            jObject["map"] = _gameMapManager.GetSelectedMap()?.MapName;
            jObject["round_id"] = RoundId;
            // Harmony start - remove queue members from the reported player count
            // jObject["players"] = _cfg.GetCVar(CCVars.AdminsCountInReportedPlayerCount)
            //     ? _playerManager.PlayerCount
            //     : _playerManager.PlayerCount - _adminManager.ActiveAdmins.Count();
            jObject["players"] = playerCount;
            // Harmony end
            jObject["soft_max_players"] = Cfg.GetCVar(CCVars.SoftMaxPlayers);
            jObject["panic_bunker"] = Cfg.GetCVar(CCVars.PanicBunkerEnabled);
            jObject["run_level"] = (int) RunLevel;
            if (preset != null)
                jObject["preset"] = (Decoy == null) ? Loc.GetString(preset.ModeTitle) : Loc.GetString(Decoy.ModeTitle);
            if (RunLevel >= GameRunLevel.InRound)
            {
                jObject["round_start_time"] = _roundStartDateTime.ToString("o");
            }
        }
    }
}
