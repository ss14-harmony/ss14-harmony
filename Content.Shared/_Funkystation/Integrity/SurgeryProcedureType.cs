namespace Content.Shared.Medical.Integrity;

/// <summary>
/// Procedure type indices for contextual integrity penalties. Used to clear penalties by procedure without string comparison.
/// </summary>
public enum SurgeryProcedureType
{
    SkinRetraction = 0,
    TissueRetraction = 1,
    BoneSawing = 2,
    BoneSmashing = 3,
    DirtyRoom = 4,
    ImproperTools = 5,
    OrganRemoval = 6,
    OrganInsertion = 7,
    LimbDetach = 8,
    LimbAttach = 9,
}
