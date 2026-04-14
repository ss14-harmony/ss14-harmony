// SPDX-FileCopyrightText: 2026 pathetic meowmeow <uhhadd@gmail.com>
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

/// <summary>
/// Marker prototype that defines well-known types of organs, e.g. "kidneys" or "left arm".
/// </summary>
[Prototype]
public sealed partial class OrganCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localized display name for this category (e.g. "Left Leg", "Left Arm").
    /// When null, falls back to ID for display.
    /// </summary>
    [DataField]
    public LocId? Name { get; private set; }

    /// <summary>
    /// Default removal procedures for organs in this category when OrganSurgeryProceduresComponent is absent.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>>? DefaultRemovalProcedures { get; private set; }

    /// <summary>
    /// Default insertion procedures for organs in this category when OrganSurgeryProceduresComponent is absent.
    /// </summary>
    [DataField]
    public List<ProtoId<SurgeryProcedurePrototype>>? DefaultInsertionProcedures { get; private set; }
}
