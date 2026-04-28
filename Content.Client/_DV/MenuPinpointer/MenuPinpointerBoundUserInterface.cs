using Content.Shared.Pinpointer;
using Content.Shared.Interaction;
using Content.Shared.IdentityManagement;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Robust.Shared.Prototypes;
using Microsoft.VisualBasic;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Robust.Client.UserInterface.Controllers;
using Content.Client.Movement.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;



// this is a list of things from the varyinng programs because i a, unsure which exactly is usefull 

namespace Content.Client._DV.MenuPinpointer;
public sealed class MenuPinpointerBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;

    [ViewVariables]
    private MenuPinpointerTargetWindow? _window;
    private EntityUid pinUid;
    public MenuPinpointerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        pinUid = owner;
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MenuPinpointerTargetWindow>();
        
        _window.OnTargetSelect += OnTargetSelected;
        _window.OnTargetSelect += OnToggleSelected;
    }
    
    //<summary>uses the given name to find the person with that name and tracks them
    private void OnTargetSelected(String targetName)
    {
        if (targetName == "")
            return;
        var target = pinUid;
        var pinpointer = _entities.GetComponent<PinpointerComponent>(pinUid);
        var PinpointerSystem = _entities.System<SharedPinpointerSystem>();
        //goes through every player looking for someone with the same name, once they do they set the target uid to this one and 
        var query = _entities.EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var uid,out _))
        {
            var possibleTargetName = Identity.Name(uid, _entities);
            if(possibleTargetName == targetName)
            {
                target = uid;
                break;
            }
        }
        if (target == pinUid)
            return;
        PinpointerSystem.SetTarget(pinUid, target, pinpointer);
    }

    //<summary> toggles the pinpointer and runs set activate without runing regular code because that 
    private void OnToggleSelected()
    {
        var pinpointer = _entities.GetComponent<PinpointerComponent>(pinUid);
        var PinpointerSystem = _entities.System<SharedPinpointerSystem>();
        var isActive = !pinpointer.IsActive;
        PinpointerSystem.SetActive(pinpointer, isActive);
        PinpointerSystem.UpdateAppearance(pinpointer);
    }
}
