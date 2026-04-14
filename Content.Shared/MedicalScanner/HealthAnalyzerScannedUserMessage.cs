// SPDX-FileCopyrightText: 2023-2024 Whisper <121047731+QuietlyWhisper@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Saphire Lattice <lattice@saphi.re>
// SPDX-FileCopyrightText: 2024 Rainfey <rainfey0+github@gmail.com>
// SPDX-FileCopyrightText: 2026 Fruitsalad <949631+Fruitsalad@users.noreply.github.com>
// SPDX-License-Identifier: MIT

using System.Linq;
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
/// On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public HealthAnalyzerUiState State;

    public HealthAnalyzerScannedUserMessage(HealthAnalyzerUiState state)
    {
        State = state;
    }
}

/// <summary>
/// Contains the current state of a health analyzer control. Used for the health analyzer and cryo pod.
/// </summary>
[Serializable, NetSerializable]
public struct HealthAnalyzerUiState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    public HealthAnalyzerMode Mode;
    public List<NetEntity> BodyParts;
    public int? IntegrityTotal;
    public int? IntegrityMax;
    public List<IntegrityPenaltyDisplayEntry> IntegrityPenaltyEntries;
    public List<SurgeryLayerStateData> BodyPartLayerState;

    public HealthAnalyzerUiState()
    {
        Mode = HealthAnalyzerMode.Health;
        BodyParts = new List<NetEntity>();
        IntegrityPenaltyEntries = new List<IntegrityPenaltyDisplayEntry>();
        BodyPartLayerState = new List<SurgeryLayerStateData>();
    }

    public HealthAnalyzerUiState(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable)
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        Mode = HealthAnalyzerMode.Health;
        BodyParts = new List<NetEntity>();
        IntegrityPenaltyEntries = new List<IntegrityPenaltyDisplayEntry>();
        BodyPartLayerState = new List<SurgeryLayerStateData>();
    }
}

[Serializable, NetSerializable]
public enum HealthAnalyzerMode : byte
{
    Health,
    Integrity,
    Surgery
}

[Serializable, NetSerializable]
public struct IntegrityPenaltyDisplayEntry
{
    public string Description;
    public int Amount;
    /// <summary>
    /// Nested entries for hierarchical display (e.g. limb with retracted skin -1, retracted tissue -1).
    /// </summary>
    public List<IntegrityPenaltyDisplayEntry>? Children;
}

[Serializable, NetSerializable]
public struct OrganInBodyPartData
{
    public NetEntity Organ;
    public string? CategoryId;
}

[Serializable, NetSerializable]
public struct OrganStepAvailability
{
    public string StepId;
    public NetEntity Organ;
}

[Serializable, NetSerializable]
public struct SurgeryProcedureState
{
    public string StepId;
    public bool Performed;
}

[Serializable, NetSerializable]
public struct SurgeryLayerStateData
{
    public NetEntity BodyPart;
    public string? CategoryId;
    public List<SurgeryProcedureState> SkinProcedures;
    public List<SurgeryProcedureState> TissueProcedures;
    public List<SurgeryProcedureState> OrganProcedures;
    public List<OrganInBodyPartData> Organs;
    public List<string> EmptySlots;
    public bool SkinOpen;
    public bool TissueOpen;
    public bool OrganOpen;

    /// <summary>
    /// Server-computed list of step IDs that can be performed. Client displays these.
    /// </summary>
    public List<string> AvailableStepIds;

    /// <summary>
    /// Full ordered list of skin step IDs for display. Unavailable steps shown greyed.
    /// </summary>
    public List<string> OrderedSkinStepIds;

    /// <summary>
    /// Full ordered list of tissue step IDs for display. Unavailable steps shown greyed.
    /// </summary>
    public List<string> OrderedTissueStepIds;

    /// <summary>
    /// Organ-qualified steps (stepId, organ) for organ removal/insertion procedures.
    /// </summary>
    public List<OrganStepAvailability> AvailableOrganSteps;

    public SurgeryLayerStateData()
    {
        SkinProcedures = new();
        TissueProcedures = new();
        OrganProcedures = new();
        Organs = new();
        EmptySlots = new();
        AvailableStepIds = new();
        OrderedSkinStepIds = new();
        OrderedTissueStepIds = new();
        AvailableOrganSteps = new();
    }

    public bool SkinRetracted => SkinOpen;
    public bool TissueRetracted => TissueOpen;
    public bool BonesSawed => OrganOpen;
}
