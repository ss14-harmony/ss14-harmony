using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Preferences.Managers;
using Content.Shared._Harmony.ReadyManifest;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Harmony.ReadyManifest;

public sealed class ReadyManifestSystem : SharedReadyManifestSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private readonly Dictionary<ICommonSession, ReadyManifestEui> _openEuis = new();
    private Dictionary<ProtoId<JobPrototype>, ReadyManifestJobData> _jobCounts = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerToggledReadyEvent>(OnPlayerToggledReady);
        SubscribeLocalEvent<PlayerDisconnectedEvent>(OnPlayerDisconnected);
        SubscribeNetworkEvent<RequestReadyManifestMessage>(OnRequestReadyManifest);
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        foreach (var (_, eui) in _openEuis)
        {
            eui.Close();
        }

        _openEuis.Clear();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _jobCounts.Clear();
    }

    private void OnPlayerToggledReady(ref PlayerToggledReadyEvent args)
    {
        // Rebuild the entire ready manifest because I can't directly update the values since it would be too likely to
        // desync, and when I thought about ways to rebuild only the updated jobs, it seemed more
        // expensive than just rebuilding the entire ready manifest.
        RebuildReadyManifest();
        UpdateAllEuis();
    }

    private void OnPlayerDisconnected(ref PlayerDisconnectedEvent args)
    {
        RebuildReadyManifest();
        UpdateAllEuis();
    }

    private void OnRequestReadyManifest(RequestReadyManifestMessage message, EntitySessionEventArgs args)
    {
        if (!_cfg.GetCVar(CCVars.CrewManifestWithoutEntity))
            return;

        OpenEui(args.SenderSession);
    }

    public void OpenEui(ICommonSession session)
    {
        if (_openEuis.ContainsKey(session))
            return;

        var eui = new ReadyManifestEui(this);
        _openEuis.Add(session, eui);
        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    public void UpdateAllEuis()
    {
        foreach (var (_, eui) in _openEuis)
        {
            eui.StateDirty();
        }
    }

    public void CloseEui(ICommonSession session)
    {
        if (!_openEuis.Remove(session, out var eui))
            return;

        eui.Close();
    }

    public Dictionary<ProtoId<JobPrototype>, ReadyManifestJobData> GetReadyManifest()
    {
        return _jobCounts;
    }

    private void RebuildReadyManifest()
    {
        _jobCounts.Clear();

        foreach (var (userId, status) in _gameTicker.PlayerGameStatuses)
        {
            if (status != PlayerGameStatus.ReadyToPlay)
                continue;

            if (!_prefsManager.TryGetCachedPreferences(userId, out var preferences))
                continue;

            var profile = (HumanoidCharacterProfile)preferences.SelectedCharacter;
            foreach (var (jobId, priority) in profile.JobPriorities)
            {
                var jobPriorityAmounts = _jobCounts.GetValueOrDefault(jobId);

                switch (priority)
                {
                    case JobPriority.High:
                        jobPriorityAmounts.HighReadies += 1;
                        break;
                    case JobPriority.Medium:
                        jobPriorityAmounts.MediumReadies += 1;
                        break;
                    case JobPriority.Low:
                        jobPriorityAmounts.LowReadies += 1;
                        break;
                }

                _jobCounts[jobId] = jobPriorityAmounts;
            }
        }
    }
}
