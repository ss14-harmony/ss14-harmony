using System.Collections.Generic;
using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Cybernetics.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Contraband;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Overlays;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics.Systems;

public sealed class CyberEyesSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Eyes = "Eyes";
    private const float CyberEyesProtectionTime = 10f;
    private const float LowEffectivenessBlurMagnitude = 0.15f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, GetBlindnessDurationMultiplierEvent>(OnGetBlindnessDurationMultiplier);
        SubscribeLocalEvent<BodyComponent, GetEyeProtectionEvent>(OnGetCyberEyesProtection);
        SubscribeLocalEvent<BodyComponent, GetBlurEvent>(OnGetCyberEyesBlur);
        SubscribeLocalEvent<BodyComponent, OrganGotInsertedEvent>(OnOrganChanged);
        SubscribeLocalEvent<BodyComponent, OrganGotRemovedEvent>(OnOrganChanged);
    }

    private void OnGetBlindnessDurationMultiplier(Entity<BodyComponent> ent, ref GetBlindnessDurationMultiplierEvent args)
    {
        var eyes = _body.GetAllOrgans(ent).FirstOrDefault(o =>
            TryComp<OrganComponent>(o, out var oc) && oc.Category == Eyes);
        if (eyes == default || !TryComp<CyberOrganComponent>(eyes, out var cyberEyes))
            return;

        args.Multiplier *= 1f / cyberEyes.Effectiveness;
    }

    private void OnGetCyberEyesProtection(Entity<BodyComponent> ent, ref GetEyeProtectionEvent args)
    {
        var eyes = _body.GetAllOrgans(ent).FirstOrDefault(o =>
            TryComp<OrganComponent>(o, out var oc) && oc.Category == Eyes);
        if (eyes == default || !TryComp<CyberOrganComponent>(eyes, out var cyberEyes) || cyberEyes.Effectiveness < 1.2f)
            return;

        args.Protection += TimeSpan.FromSeconds(CyberEyesProtectionTime);
    }

    private void OnGetCyberEyesBlur(Entity<BodyComponent> ent, ref GetBlurEvent args)
    {
        var eyes = _body.GetAllOrgans(ent).FirstOrDefault(o =>
            TryComp<OrganComponent>(o, out var oc) && oc.Category == Eyes);
        if (eyes == default || !TryComp<CyberOrganComponent>(eyes, out var cyberEyes) || cyberEyes.Effectiveness > 0.8f)
            return;

        args.Blur += (1f - cyberEyes.Effectiveness) * LowEffectivenessBlurMagnitude;
    }

    private void OnOrganChanged(Entity<BodyComponent> ent, ref OrganGotInsertedEvent args)
    {
        UpdateCyberEyesHud(ent);
    }

    private void OnOrganChanged(Entity<BodyComponent> ent, ref OrganGotRemovedEvent args)
    {
        UpdateCyberEyesHud(ent);
    }

    private void UpdateCyberEyesHud(Entity<BodyComponent> ent)
    {
        var eyes = _body.GetAllOrgans(ent).FirstOrDefault(o =>
            TryComp<OrganComponent>(o, out var oc) && oc.Category == Eyes);
        if (eyes == default || !TryComp<CyberOrganComponent>(eyes, out var cyberEyes) || cyberEyes.Effectiveness < 1.4f)
        {
            RemCompDeferred<CyberEyesHudComponent>(ent);
            RemCompDeferred<ShowHealthBarsComponent>(ent);
            RemCompDeferred<ShowHealthIconsComponent>(ent);
            RemCompDeferred<ShowJobIconsComponent>(ent);
            RemCompDeferred<ShowMindShieldIconsComponent>(ent);
            RemCompDeferred<ShowCriminalRecordIconsComponent>(ent);
            RemCompDeferred<ShowContrabandDetailsComponent>(ent);
            return;
        }

        EnsureComp<CyberEyesHudComponent>(ent);
        var healthBars = EnsureComp<ShowHealthBarsComponent>(ent);
        healthBars.DamageContainers = new List<ProtoId<DamageContainerPrototype>> { "Biological", "Inorganic" };
        var healthIcons = EnsureComp<ShowHealthIconsComponent>(ent);
        healthIcons.DamageContainers = new List<ProtoId<DamageContainerPrototype>> { "Biological", "Inorganic" };
        EnsureComp<ShowJobIconsComponent>(ent);
        EnsureComp<ShowMindShieldIconsComponent>(ent);
        EnsureComp<ShowCriminalRecordIconsComponent>(ent);
        EnsureComp<ShowContrabandDetailsComponent>(ent);
    }
}
