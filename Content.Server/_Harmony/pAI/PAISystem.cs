using Content.Shared.PAI;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;

namespace Content.Server.pAI;

public sealed class PAISystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PAIComponent, EncryptionChannelsChangedEvent>(OnKeysChanged); // Harmony Additions: Added more entries into the pAI store
    }

    private void OnKeysChanged(EntityUid uid, PAIComponent component, EncryptionChannelsChangedEvent args)
    {
        UpdateRadioChannels(uid, component, args.Component);
    }
    private void UpdateRadioChannels(EntityUid uid, PAIComponent innate, EncryptionKeyHolderComponent keyHolder)
    {
        foreach (var channel in innate.Channels)
        {
            keyHolder.Channels.Add(channel);
        }
        EnsureComp<ActiveRadioComponent>(uid).Channels = keyHolder.Channels;
    }
}
