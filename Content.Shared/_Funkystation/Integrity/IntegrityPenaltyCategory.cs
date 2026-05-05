namespace Content.Shared.Medical.Integrity;

/// <summary>
/// Category for body-level contextual integrity penalties (dirty room, improper tools).
/// Used to clear penalties by category without string comparison.
/// </summary>
public enum IntegrityPenaltyCategory
{
    DirtyRoom = 0,
    ImproperTools = 1,
    UnsanitarySurgery = 2,
}
