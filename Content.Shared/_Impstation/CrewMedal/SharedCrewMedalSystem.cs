using Content.Shared.Examine;

namespace Content.Shared._Impstation.CrewMedal;

public abstract class SharedCrewMedalSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMedalComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CrewMedalComponent> medal, ref ExaminedEvent args)
    {
        if (!medal.Comp.Awarded)
            return;

        // Harmony Change Start - UI Formatting Change
        var localAwardString = "comp-crew-medal-inspection-text";
        if (medal.Comp.Reason != String.Empty)
            localAwardString = "comp-crew-medal-inspection-text-with-reason";
        var str = Loc.GetString(localAwardString, ("recipient", medal.Comp.Recipient), ("reason", medal.Comp.Reason));
        // Harmony Change End
        args.PushMarkup(str);
    }
}
