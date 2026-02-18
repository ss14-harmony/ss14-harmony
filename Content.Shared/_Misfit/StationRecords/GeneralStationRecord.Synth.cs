namespace Content.Shared.StationRecords;

public sealed partial record GeneralStationRecord
{
    /// <summary>
    ///     Misfit - Synthetic trait for searching support
    /// </summary>
    [DataField]
    public bool Synthetic;
}
