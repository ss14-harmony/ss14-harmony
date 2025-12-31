using Content.Shared._Harmony.BindSoul;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Harmony.BindSoul;

public sealed class BindSoulSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public readonly ProtoId<ShaderPrototype> Shader = "BindSoul";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoulBindedComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(Entity<SoulBindedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.Color = Color.DarkGreen;
    }
}
