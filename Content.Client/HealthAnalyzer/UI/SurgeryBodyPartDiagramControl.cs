using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.MedicalScanner;
using Content.Shared.Preferences;
using Content.Shared.Rotation;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.IoC;
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

    /// <summary>
    /// When true, clicks on the diagram select body parts. When false, clicks are ignored (e.g. in Health/Integrity modes).
    /// </summary>
    public bool IsClickable
    {
        get => _isClickable;
        set => _isClickable = value;
    }

    // Regions aligned for a centered, Fit-scaled sprite facing the player (Direction.South).
    // When facing the player, the sprite's left (low X) = patient's right side, so we swap labels.
    // Order: parent limbs (arms, legs) before child parts (hands, feet) so clicking an arm/leg
    // selects the whole limb for DetachLimb, not just the hand/foot.
    private static readonly (string CategoryId, Box2 NormalizedRect)[] RegionMap =
    {
        ("Head", new Box2(0.25f, 0.15f, 0.75f, 0.38f)),
        ("ArmRight", new Box2(0, 0.35f, 0.42f, 0.78f)),      // extended toward torso to eliminate gap
        ("ArmLeft", new Box2(0.58f, 0.35f, 1, 0.78f)),       // extended toward torso to eliminate gap
        ("HandRight", new Box2(0, 0.58f, 0.22f, 0.78f)),      // sprite left = patient right
        ("HandLeft", new Box2(0.78f, 0.58f, 1, 0.78f)),       // sprite right = patient left
        ("LegRight", new Box2(0.25f, 0.68f, 0.5f, 1)),
        ("LegLeft", new Box2(0.5f, 0.68f, 0.75f, 1)),
        ("FootRight", new Box2(0.25f, 0.82f, 0.5f, 1)),
        ("FootLeft", new Box2(0.5f, 0.82f, 0.75f, 1)),
        ("Torso", new Box2(0.42f, 0.40f, 0.58f, 0.65f)),
    };

    /// <summary>
    /// Exposed for integration tests to verify region order (arms before hands, legs before feet).
    /// </summary>
    internal static IReadOnlyList<string> RegionCategoryOrder => RegionMap.Select(r => r.CategoryId).ToArray();

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

        // Overlay drawn on top of sprite; both fill container so overlay renders last and on top
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
        // Only clear selection when target changes; preserve when refreshing same patient
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

        // Reuse existing preview when target unchanged; refresh appearance to pick up limb visibility updates
        if (_previewEntity != null && prevTarget == target)
        {
            RefreshPreviewAppearance(patient.Value);
            return;
        }

        if (_previewEntity != null)
        {
            _entManager.DeleteEntity(_previewEntity.Value);
            _previewEntity = null;
        }

        _previewEntity = SpawnStandingPreview(patient.Value);
        _spriteView?.SetEntity(_previewEntity.Value);
    }

    /// <summary>
    /// Syncs preview body appearance and organ structure to match patient (limb visibility from organ presence).
    /// </summary>
    private void RefreshPreviewAppearance(EntityUid patient)
    {
        if (_previewEntity == null)
            return;
        var visualBody = _entManager.System<SharedVisualBodySystem>();
        if (_entManager.TryGetComponent(patient, out BodyComponent? patientBody) &&
            _entManager.TryGetComponent(_previewEntity.Value, out BodyComponent? previewBody))
        {
            SyncPreviewBodyToPatient(patient, patientBody, _previewEntity.Value, previewBody);
            visualBody.CopyAppearanceFrom((patient, patientBody), (_previewEntity.Value, previewBody));
        }
    }

    private void SyncPreviewBodyToPatient(EntityUid patient, BodyComponent patientBody, EntityUid preview, BodyComponent previewBody)
    {
        var containerSystem = _entManager.System<Robust.Shared.Containers.SharedContainerSystem>();
        if (!containerSystem.TryGetContainer(patient, BodyComponent.ContainerID, out var patientContainer) ||
            !containerSystem.TryGetContainer(preview, BodyComponent.ContainerID, out var previewContainer))
            return;

        var patientCategories = new HashSet<string>();
        foreach (var ent in patientContainer.ContainedEntities)
        {
            if (_entManager.TryGetComponent(ent, out OrganComponent? organ) && organ.Category is { } cat)
                patientCategories.Add(cat.ToString());
        }

        foreach (var ent in previewContainer.ContainedEntities.ToArray())
        {
            if (_entManager.TryGetComponent(ent, out OrganComponent? organ) && organ.Category is { } cat)
            {
                if (!patientCategories.Contains(cat.ToString()))
                {
                    // Preview is in Nullspace; reparenting would fail. Remove without reparent and delete.
                    containerSystem.Remove(ent, previewContainer, reparent: false, force: true);
                    _entManager.DeleteEntity(ent);
                }
                else if (_entManager.TryGetComponent(ent, out BodyPartComponent? previewPart) && previewPart.Organs != null)
                {
                    // Sync nested organs. Cyber limbs have combined arm+hand (no separate HandLeft/HandRight);
                    // remove preview's hand/foot organs that the patient doesn't have.
                    var patientPart = patientContainer.ContainedEntities
                        .FirstOrDefault(e => _entManager.TryGetComponent(e, out OrganComponent? o) && o.Category == organ.Category);
                    var patientPartCategories = new HashSet<string>();
                    if (patientPart != default && _entManager.TryGetComponent(patientPart, out BodyPartComponent? patientPartComp) && patientPartComp.Organs != null)
                    {
                        foreach (var patientChild in patientPartComp.Organs.ContainedEntities)
                        {
                            if (_entManager.TryGetComponent(patientChild, out OrganComponent? o) && o.Category is { } c)
                                patientPartCategories.Add(c.ToString());
                        }
                    }
                    foreach (var previewChild in previewPart.Organs.ContainedEntities.ToArray())
                    {
                        if (_entManager.TryGetComponent(previewChild, out OrganComponent? o) && o.Category is { } c)
                        {
                            if (!patientPartCategories.Contains(c.ToString()))
                            {
                                containerSystem.Remove(previewChild, previewPart.Organs, reparent: false, force: true);
                                _entManager.DeleteEntity(previewChild);
                            }
                        }
                    }
                }
            }
        }
    }

    private EntityUid SpawnStandingPreview(EntityUid patient)
    {
        var species = HumanoidCharacterProfile.DefaultSpecies;
        if (_entManager.TryGetComponent(patient, out HumanoidProfileComponent? humanoidProfile))
            species = humanoidProfile.Species;

        EntityUid preview;
        if (_prototypes.TryIndex<SpeciesPrototype>(species, out var speciesProto))
            preview = _entManager.SpawnEntity(speciesProto.DollPrototype, MapCoordinates.Nullspace);
        else
            preview = _entManager.SpawnEntity("MobHuman", MapCoordinates.Nullspace);

        var visualBody = _entManager.System<SharedVisualBodySystem>();
        if (_entManager.TryGetComponent(patient, out BodyComponent? patientBody) &&
            _entManager.TryGetComponent(preview, out BodyComponent? previewBody))
        {
            SyncPreviewBodyToPatient(patient, patientBody, preview, previewBody);
            visualBody.CopyAppearanceFrom((patient, patientBody), (preview, previewBody));
        }

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

        // Only consider clicks inside the sprite (Fit mode = sprite doesn't fill control)
        if (relPos.X < r.Left || relPos.X > r.Right || relPos.Y < r.Top || relPos.Y > r.Bottom)
            return;

        // Convert to sprite-normalized (0-1) for region lookup
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

    /// <summary>
    /// Returns the sprite's bounding rect in control pixel space, or null if no sprite.
    /// Used to convert between control coords and sprite-normalized (0-1) coords for regions.
    /// </summary>
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

    /// <summary>
    /// Replicates SpriteView's transform logic for Fit mode. Matches SpriteView.Draw (Size, stretch, position, scale).
    /// Returns position (center in pixels), layerScale (converts meter bounds to pixels), spriteSize (in pixels), and spriteScale (UIScale*stretch for sprite rect).
    /// </summary>
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
        // Layer bounds from GetLocalBounds are in meters; scale converts to control pixels
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

        // Map region from sprite-normalized (0-1) to sprite pixel rect
        // Box2 uses (Left, Bottom, Right, Top) with Y+ up; UIBox2 uses (Left, Top, Right, Bottom) with Y+ down.
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

    /// <summary>
    /// Draws the body part overlay on top of the sprite. Must be a sibling after SpriteView
    /// so it renders on top (children are drawn after the control's own Draw).
    /// </summary>
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
