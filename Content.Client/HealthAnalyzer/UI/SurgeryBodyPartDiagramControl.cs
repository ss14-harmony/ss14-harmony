using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Rotation;
using Content.Shared.MedicalScanner;
using Content.Shared.Rotation;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.HealthAnalyzer.UI;

/// <summary>
/// A clickable body part diagram that displays a standing preview of the patient.
/// Click regions map to body parts (Torso, Head, ArmLeft, ArmRight, LegLeft, LegRight).
/// Selected region is highlighted in red.
/// </summary>
public sealed class SurgeryBodyPartDiagramControl : Control
{
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypes;
    private EntityUid? _previewEntity;
    private SpriteView? _spriteView;
    private BodyPartOverlayControl? _overlayControl;
    private NetEntity? _targetEntity;
    private List<SurgeryLayerStateData> _bodyPartLayerState = new();
    private NetEntity? _selectedBodyPart;
    private bool _isClickable = true;

    public bool IsClickable
    {
        get => _isClickable;
        set => _isClickable = value;
    }

    private static readonly (string CategoryId, Box2 NormalizedRect)[] RegionMap =
    {
        ("Head", new Box2(0.25f, 0.15f, 0.75f, 0.38f)),
        ("ArmRight", new Box2(0, 0.35f, 0.42f, 0.78f)),
        ("ArmLeft", new Box2(0.58f, 0.35f, 1, 0.78f)),
        ("HandRight", new Box2(0, 0.58f, 0.22f, 0.78f)),
        ("HandLeft", new Box2(0.78f, 0.58f, 1, 0.78f)),
        ("LegRight", new Box2(0.25f, 0.68f, 0.5f, 1)),
        ("LegLeft", new Box2(0.5f, 0.68f, 0.75f, 1)),
        ("FootRight", new Box2(0.25f, 0.82f, 0.5f, 1)),
        ("FootLeft", new Box2(0.5f, 0.82f, 0.75f, 1)),
        ("Torso", new Box2(0.42f, 0.40f, 0.58f, 0.65f)),
    };

    private static readonly IReadOnlyDictionary<string, HumanoidVisualLayers[]> CategoryToLayers = new Dictionary<string, HumanoidVisualLayers[]>
    {
        ["Torso"] = [HumanoidVisualLayers.Chest],
        ["Head"] = [HumanoidVisualLayers.Head],
        ["ArmLeft"] = [HumanoidVisualLayers.LArm, HumanoidVisualLayers.LHand],
        ["ArmRight"] = [HumanoidVisualLayers.RArm, HumanoidVisualLayers.RHand],
        ["HandLeft"] = [HumanoidVisualLayers.LHand],
        ["HandRight"] = [HumanoidVisualLayers.RHand],
        ["LegLeft"] = [HumanoidVisualLayers.LLeg, HumanoidVisualLayers.LFoot],
        ["LegRight"] = [HumanoidVisualLayers.RLeg, HumanoidVisualLayers.RFoot],
        ["FootLeft"] = [HumanoidVisualLayers.LFoot],
        ["FootRight"] = [HumanoidVisualLayers.RFoot],
    };

    public Action<NetEntity>? OnBodyPartSelected;

    public SurgeryBodyPartDiagramControl()
    {
        _entManager = IoCManager.Resolve<IEntityManager>();
        _prototypes = IoCManager.Resolve<IPrototypeManager>();

        MinSize = new Vector2(64, 128);
        SetSize = new Vector2(128, 256);

        MouseFilter = Control.MouseFilterMode.Stop;

        var container = new LayoutContainer();
        LayoutContainer.SetAnchorPreset(container, LayoutContainer.LayoutPreset.Wide);

        _spriteView = new SpriteView
        {
            OverrideDirection = Robust.Shared.Maths.Direction.South,
            Scale = new Vector2(2, 2),
            Stretch = SpriteView.StretchMode.Fit,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = Control.MouseFilterMode.Ignore
        };
        LayoutContainer.SetAnchorPreset(_spriteView, LayoutContainer.LayoutPreset.Wide);
        container.AddChild(_spriteView);

        _overlayControl = new BodyPartOverlayControl(this);
        LayoutContainer.SetAnchorPreset(_overlayControl, LayoutContainer.LayoutPreset.Wide);
        container.AddChild(_overlayControl);

        AddChild(container);
    }

    public void SetTarget(NetEntity? target, List<SurgeryLayerStateData> bodyPartLayerState)
    {
        var prevTarget = _targetEntity;
        _targetEntity = target;
        _bodyPartLayerState = bodyPartLayerState ?? new List<SurgeryLayerStateData>();
        if (prevTarget != target || target == null)
            _selectedBodyPart = null;

        if (target == null || !_entManager.TryGetEntity(target, out var patient))
        {
            if (_previewEntity != null)
            {
                _entManager.DeleteEntity(_previewEntity.Value);
                _previewEntity = null;
            }
            _spriteView?.SetEntity(null);
            return;
        }

        if (_previewEntity != null && prevTarget == target)
            return;

        if (_previewEntity != null)
        {
            _entManager.DeleteEntity(_previewEntity.Value);
            _previewEntity = null;
        }

        _previewEntity = SpawnStandingPreview(patient.Value);
        _spriteView?.SetEntity(_previewEntity.Value);
    }

    private EntityUid SpawnStandingPreview(EntityUid patient)
    {
        var species = Content.Shared.Preferences.HumanoidCharacterProfile.DefaultSpecies;
        if (_entManager.TryGetComponent(patient, out HumanoidProfileComponent? profile))
            species = profile.Species;

        EntityUid preview;
        if (_prototypes.TryIndex<SpeciesPrototype>(species, out var speciesProto))
            preview = _entManager.SpawnEntity(speciesProto.DollPrototype, MapCoordinates.Nullspace);
        else
            preview = _entManager.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

        if (_entManager.TryGetComponent(preview, out AppearanceComponent? appearance))
        {
            _entManager.System<Robust.Client.GameObjects.AppearanceSystem>().SetData(preview, RotationVisuals.RotationState, RotationState.Vertical, appearance);
        }

        var spriteSystem = _entManager.System<Robust.Client.GameObjects.SpriteSystem>();
        if (_entManager.TryGetComponent(preview, out SpriteComponent? spriteComp))
        {
            spriteSystem.SetRotation((preview, spriteComp), Angle.Zero);
        }

        return preview;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!_isClickable || args.Function != EngineKeyFunctions.UIClick || _spriteView == null || _bodyPartLayerState.Count == 0)
            return;

        var relPos = args.RelativePixelPosition;
        var spriteRect = GetSpriteRectInPixels();
        if (!spriteRect.HasValue)
            return;

        var r = spriteRect.Value;
        var w = r.Right - r.Left;
        var h = r.Bottom - r.Top;
        if (w <= 0 || h <= 0)
            return;

        if (relPos.X < r.Left || relPos.X > r.Right || relPos.Y < r.Top || relPos.Y > r.Bottom)
            return;

        var normalizedX = (relPos.X - r.Left) / w;
        var normalizedY = (relPos.Y - r.Top) / h;

        var point = new Vector2(normalizedX, normalizedY);
        foreach (var (categoryId, rect) in RegionMap)
        {
            if (!rect.Contains(point))
                continue;

            var match = _bodyPartLayerState.FirstOrDefault(s => s.CategoryId == categoryId);
            if (match.BodyPart != default)
            {
                _selectedBodyPart = match.BodyPart;
                OnBodyPartSelected?.Invoke(match.BodyPart);
                return;
            }
        }
    }

    internal void DrawOverlay(IRenderHandle renderHandle, Vector2 overlayPixelSize)
    {
        if (_spriteView == null || !_selectedBodyPart.HasValue || _previewEntity == null)
            return;

        var match = _bodyPartLayerState.FirstOrDefault(s => s.BodyPart == _selectedBodyPart);
        if (match.CategoryId == null)
            return;

        if (!CategoryToLayers.TryGetValue(match.CategoryId, out var layers))
            return;

        var spriteSystem = _entManager.System<SpriteSystem>();
        if (!_entManager.TryGetComponent(_previewEntity, out SpriteComponent? sprite))
        {
            DrawFallbackRect(renderHandle, match.CategoryId);
            return;
        }

        spriteSystem.ForceUpdate(_previewEntity.Value);

        var (position, layerScale, _, _) = ComputeSpriteViewTransform(sprite, spriteSystem);
        var tint = Color.Red.WithAlpha(0.6f);
        var drawnAny = false;

        foreach (var layerEnum in layers)
        {
            if (!spriteSystem.TryGetLayer((_previewEntity.Value, sprite), layerEnum, out var layer, false))
                continue;

            if (!layer.Visible || layer.Blank)
                continue;

            var texture = layer.ActualState?.GetFrame(RsiDirection.South, layer.AnimationFrame) ?? layer.Texture;
            var bounds = spriteSystem.GetLocalBounds(layer);
            bounds = bounds.Scale(sprite.Scale);

            var screenRect = TransformLocalBoundsToScreen(bounds, position, layerScale);
            if (texture != null)
            {
                renderHandle.DrawingHandleScreen.DrawTextureRectRegion(texture, screenRect, null, tint);
            }
            else
            {
                renderHandle.DrawingHandleScreen.DrawRect(screenRect, tint);
            }
            drawnAny = true;
        }

        if (!drawnAny)
            DrawFallbackRect(renderHandle, match.CategoryId);
    }

    private UIBox2? GetSpriteRectInPixels()
    {
        if (_previewEntity == null || !_entManager.TryGetComponent(_previewEntity, out SpriteComponent? sprite))
            return null;

        var spriteSystem = _entManager.System<SpriteSystem>();
        var (position, _, spriteSize, spriteScale) = ComputeSpriteViewTransform(sprite, spriteSystem);

        var halfW = spriteSize.X * spriteScale / 2;
        var halfH = spriteSize.Y * spriteScale / 2;
        return new UIBox2(
            position.X - halfW, position.Y - halfH,
            position.X + halfW, position.Y + halfH);
    }

    private (Vector2 position, Vector2 layerScale, Vector2 spriteSize, float spriteScale) ComputeSpriteViewTransform(SpriteComponent sprite, SpriteSystem spriteSystem)
    {
        var spriteBox = sprite.CalculateRotatedBoundingBox(default, Angle.Zero, Angle.Zero).CalcBoundingBox();
        spriteBox = spriteBox.Translated(-spriteBox.Center);

        var viewScale = new Vector2(2, 2);
        var scale = viewScale * (float)EyeManager.PixelsPerMeter;
        var bl = spriteBox.BottomLeft * scale;
        var tr = spriteBox.TopRight * scale;
        tr = Vector2.Max(tr, Vector2.Zero);
        bl = Vector2.Min(bl, Vector2.Zero);
        tr = Vector2.Max(tr, -bl);
        bl = Vector2.Min(bl, -tr);
        var box = new Box2(bl, tr);
        var spriteSize = box.Size;

        var stretchVec = Vector2.Min(Size / spriteSize, Vector2.One);
        var stretch = MathF.Min(stretchVec.X, stretchVec.Y);

        var offset = Vector2.Zero;
        var position = PixelSize / 2 + offset * stretch * UIScale;
        var spriteScale = UIScale * stretch;
        var layerScale = viewScale * (float)EyeManager.PixelsPerMeter * spriteScale;

        return (position, layerScale, spriteSize, spriteScale);
    }

    private static UIBox2 TransformLocalBoundsToScreen(Box2 localBounds, Vector2 position, Vector2 scale)
    {
        var left = position.X + localBounds.Left * scale.X;
        var right = position.X + localBounds.Right * scale.X;
        var top = position.Y - localBounds.Top * scale.Y;
        var bottom = position.Y - localBounds.Bottom * scale.Y;
        return new UIBox2(left, top, right, bottom);
    }

    private void DrawFallbackRect(IRenderHandle renderHandle, string categoryId)
    {
        var (_, rect) = RegionMap.FirstOrDefault(r => r.CategoryId == categoryId);
        if (rect == default)
            return;

        var spriteRect = GetSpriteRectInPixels();
        if (!spriteRect.HasValue)
            return;

        var r = spriteRect.Value;
        var w = r.Right - r.Left;
        var h = r.Bottom - r.Top;
        if (w <= 0 || h <= 0)
            return;

        var left = r.Left + rect.Left * w;
        var right = r.Left + rect.Right * w;
        var top = r.Top + rect.Bottom * h;
        var bottom = r.Top + rect.Top * h;

        var screenRect = new UIBox2(left, top, right, bottom);
        renderHandle.DrawingHandleScreen.DrawRect(screenRect, Color.Red.WithAlpha(0.6f));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_previewEntity != null)
        {
            _entManager.DeleteEntity(_previewEntity.Value);
            _previewEntity = null;
        }
    }

    public void SetSelectedBodyPart(NetEntity? bodyPart)
    {
        _selectedBodyPart = bodyPart;
    }

    private sealed class BodyPartOverlayControl : Control
    {
        private readonly SurgeryBodyPartDiagramControl _owner;

        public BodyPartOverlayControl(SurgeryBodyPartDiagramControl owner)
        {
            _owner = owner;
            MouseFilter = Control.MouseFilterMode.Ignore;
            HorizontalExpand = true;
            VerticalExpand = true;
        }

        protected override void Draw(IRenderHandle renderHandle)
        {
            base.Draw(renderHandle);
            _owner.DrawOverlay(renderHandle, PixelSize);
        }
    }
}
