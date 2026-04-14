using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Shared.Cybernetics.Systems;

/// <summary>
/// Blocks wielding two-handed items (e.g. guns) when they are in cyber arm storage.
/// </summary>
public sealed class CyberArmWieldingBlockerSystem : EntitySystem
{
    [Dependency] private readonly SharedCyberArmStorageSystem _cyberArmStorage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WieldAttemptEvent>(OnWieldAttempt);
    }

    private void OnWieldAttempt(ref WieldAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<WieldableComponent>(args.Wielded, out var wieldable) || wieldable.FreeHandsRequired < 1)
            return;

        if (!_cyberArmStorage.IsInCyberArmStorage(args.Wielded, args.User))
            return;

        args.Cancel();
        args.Message = Loc.GetString("cyber-arm-cannot-wield-two-handed");
    }
}
