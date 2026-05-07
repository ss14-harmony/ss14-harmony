# Cybernetics & Surgery Systems — Stability and Crash-Risk Analysis

This document summarizes a targeted review of Funky Station's **cybernetics** and **surgery** code paths (shared, server, and client UI) with emphasis on conditions that could cause **server exceptions**, **client exceptions**, or **process termination**. It is not an exhaustive formal verification; it reflects static review of the primary systems and call chains involved in surgery requests, completion, integrity penalties, health analyzer UI, and cyber limb lifecycle.

> **Revision note:** This is a re-checked version. After tracing the actual control flow more carefully, the original "Medium" finding for `organNet!.Value` was downgraded to a defensive-coding concern (the runtime invariant was already satisfied). The corresponding code has been refactored to make the safety explicit rather than implicit. See **Findings** and **Fixes Applied** below.

## Scope (major areas reviewed)

| Area | Representative paths |
|------|------------------------|
| Surgery execution & validation | `Content.Shared/_Funkystation/Surgery/SurgerySystem.cs`, `SurgeryLayerSystem.cs` |
| Limb / organ side effects | `LimbDetachmentEffectsSystem.cs`, `SurgeryLimbTaggingSystem.cs` |
| Health analyzer + surgery UI state | `Content.Server/Medical/HealthAnalyzerSystem.cs`, `Content.Shared/MedicalScanner/*`, `Content.Client/HealthAnalyzer/UI/HealthAnalyzerControl.xaml.cs`, `Content.Client/_Funkystation/HealthAnalyzer/UI/SurgeryBodyPartDiagramControl.cs` |
| Unsanitary surgery / integrity | `Content.Server/_Funkystation/Medical/Integrity/UnsanitarySurgeryCalculationSystem.cs` |
| Cybernetics | `CyberLimbStatsSystem.cs`, `CyberLimbModuleSystem.cs`, `CyberneticsMaintenanceSystem.cs`, `CyberLimbAppearanceSystem.cs`, server `CyberLiverSystem.cs`, `CyberArmSelectSystem.cs`, `SharedCyberArmStorageSystem.cs`, `CyberneticsBatteryDrainerSystem.cs` |

RobustToolbox / engine internals were not modified or audited in depth; behavior assumes stable ECS and networking.

---

## Overall assessment

The implementation generally follows SS14 patterns: **`TryComp` / `Exists` / null-coalescing** on hot paths, **`?? []`** for UI lists, and **early returns** when bodies or parts are invalid. Most user-visible failures degrade to **rejected surgery**, **skipped updates**, or **empty UI**, rather than hard crashes.

**No definite crash bugs were identified** in normal gameplay or with valid prototypes. The areas of concern are mostly **defensive-coding / code-smell** issues where non-null assertions (`!`) lean on logic that's currently correct but fragile against future refactors.

---

## Findings by severity (re-verified)

### Defensive (was Medium) — non-null assertions in `SurgerySystem.ApplyOrganStep`

**Original concern (pre-fix):**
```cs
var removalSteps = layerComp!.OrganRemovalProgress
    .FirstOrDefault(e => e.Organ == organNet!.Value)?.Steps.ToList() ?? new List<string>();
// ...
ClearOrganRemovalProgress(layerComp, organNet!.Value);
// ...
AddOrganRemovalProgress(layerComp!, organNet!.Value, stepId);
ClearOrganInsertProgress(layerComp!, organNet!.Value);
```

**Re-traced flow:**
1. `OnSurgeryDoAfter` computes `organUid = args.Organ.HasValue ? GetEntity(args.Organ.Value) : null;`.
2. `ApplyOrganStep` enters the `triggersOrganRemoval || RemoveOrgan` branch and validates `organUid is not { } organ || !Exists(organ)` — early returns otherwise.
3. Because `organUid` is only non-null when `args.Organ.HasValue == true`, by the time the code reaches `organNet!.Value`, `organNet` (which is `args.Organ`) **must** have a value.

**Conclusion:** `organNet!.Value` was logically safe in current code. **Not a crash bug.** However, the safety was **implicit** (through `organUid` validation), and the same was true of the `bodyPartComp!` / `layerComp!` operators in nearby lines.

**Fix applied:** Replaced the implicit safety with **explicit checks** and derived the `NetEntity` from the validated `EntityUid` via `GetNetEntity(organ)`. This removes all `!.` operators in this block. See **Fixes Applied** below.

---

### Low — client `SurgeryLayerStateData` default struct vs. nullable usage

`SurgeryLayerStateData` is a **`struct`** with list fields initialized in its **parameterless constructor**. A **`default(SurgeryLayerStateData)`** from `FirstOrDefault` when no row matches the selected body part leaves **list fields null** until explicitly constructed.

Reviewed paths in `HealthAnalyzerControl.xaml.cs`:
- `GetPerformedStepIds` short-circuits with **`if (procedures == null) yield break`**.
- `AvailableStepIds ?? []`, `OrderedSkinStepIds ?? []`, `OrderedTissueStepIds ?? []`, `AvailableOrganSteps ?? []` all use null-coalescing.
- Boolean accesses like `layerState.SkinRetracted` / `TissueRetracted` / `OrganOpen` don't deref any list.

**Result: no crash path identified** in current code. Future contributors should keep treating list fields as possibly null when reading from a `default` struct.

---

### Low — surgery diagram preview lifecycle (`SurgeryBodyPartDiagramControl`)

- **`SetTarget`** uses **`DeleteEntity`** in some branches vs **`QueueDeleteEntity`** in **`CleanupPreviewFromTree`**. Mixed immediate vs deferred deletion is not a crash by itself but is inconsistent. No crash observed in review.
- **Click routing** uses **`FirstOrDefault`** with **`match.BodyPart != default`** guard.
- **Overlay drawing** falls back when sprite layers / textures are missing.

---

### Low — unsanitary penalty calculation (server)

`UnsanitarySurgeryCalculationSystem`:
- Resolves grid via **`GridUid`** or **`TryFindGridAt`**; returns early when no grid.
- **Flood fill** is **bounded by depth** (`FloodFillMaxDistance = 3`) and a **visited set**.
- **`AllocateSharesTotaling`** uses largest-remainder bucketing over a fixed length-3 array; `remainder` is bounded by rounding.
- **`PatientOnSurgeryBed`** calls `MetaData(strapEnt)` after `BuckleComponent.BuckledTo is not { } strapEnt`. If `BuckledTo` ever held a stale (deleted) reference, this could throw — but the buckle system normally clears the field on detach. Not changed; out of scope.

---

### Informational — prototype / content misconfiguration

- **`SurgeryProcedurePrototype`** requires `PrimaryTool`. Invalid YAML fails at **prototype load**, not mid-round.
- **`SurgeryLimbTaggingSystem`** assigns `CyberLimb*` steps config IDs for cyber limbs; resolution goes through **`TryIndex`**. A missing prototype yields **degraded surgery** (empty steps), not a crash.

---

### Informational — cybernetics numeric paths

- **`CyberLimbStatsSystem`**: division paths in `MaybePopupLowService` / `MaybePopupLowPower` are guarded by `<= TimeSpan.Zero` / `<= 0f` checks.
- **`CyberLimbModuleSystem.GetModuleCounts`**: only adds matter-bin entities that pass `HasComp<CyberLimbMatterBinComponent>`; the later `Comp<CyberLimbMatterBinComponent>(mb)` calls are therefore safe in the same logical tick.
- **`CyberLiverSystem`**: damage/heal rates derived from `effectiveness` factor; no division by `effectiveness`.

---

## Fixes Applied

### `SurgerySystem.ApplyOrganStep` — remove implicit non-null assertions

In the `triggersOrganRemoval || stepId == "RemoveOrgan"` branch, replaced implicit chains of `bodyPartComp!`, `layerComp!`, and `organNet!.Value` with explicit guards:

- Added explicit `bodyPartComp == null || bodyPartComp.Organs == null` check (was `bodyPartComp!.Organs == null`).
- Added explicit `if (layerComp == null)` early return with a popup, before any `layerComp.*` use.
- Derived `var organNetValue = GetNetEntity(organ);` from the **already-validated** `EntityUid organ`, eliminating all `organNet!.Value` references.

The behavior is **byte-for-byte identical** in normal flows (the resolved `NetEntity` matches the original `args.Organ`). The change defends against:
- Any future refactor that decouples `args.Organ` from the resolved `organUid`.
- Any future call path that misses the `organUid` validation.

This makes the code's safety **explicit** rather than reliant on a multi-step inference chain.

---

## Client vs server split

| Concern | Server | Client |
|--------|--------|--------|
| Surgery validity & damage | **Authoritative** — `SurgeryRequestEvent` / `SurgeryDoAfterEvent` | Prediction / feedback popups (`SharedHealthAnalyzerSystem`) |
| Integrity penalties (unsanitary, improvised) | **`UnsanitarySurgeryCalculationSystem`**, integrity events | Displays totals from replicated/analyzer state |
| Cyber limb stats / batteries | **`CyberLimbStatsSystem.Update`** gated with **`_net.IsServer`** | Client gets networked state; stats recompute events skip **`ApplyingState`** where appropriate |

---

## Remaining recommendations

1. **Stylistic:** When extending `UpdateSurgeryView` / `AddOrderedSteps`, treat `SurgeryLayerStateData` list fields as possibly null if the struct may be `default`.
2. **Stylistic:** Prefer `QueueDeleteEntity` consistently for the analyzer preview dummy entity if any ordering issues appear in testing.
3. **Watch:** `UnsanitarySurgeryCalculationSystem.PatientOnSurgeryBed` calls `MetaData(strapEnt)` directly — defensible only if buckle invariants hold. Replace with `MetaQuery.TryGetComponent` if a stale-reference crash ever appears in logs.
4. Keep the existing **integration tests** under `Content.IntegrationTests/Tests/Medical` and `Tests/Cybernetics` — they cover the full surgery and cyber limb flows.

---

## Conclusion

After re-verification, **no crash-shaped bug remains** under valid prototypes and normal gameplay. The most prominent code-smell — implicit non-null assertions in the organ-removal branch of `SurgerySystem.ApplyOrganStep` — has been refactored to make the safety explicit, which both eliminates the crash-shaped pattern and makes the code clearer to future contributors.
