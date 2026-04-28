namespace ThreeKingdom.Data;

public enum InternalAffairsJobType
{
    Farm,
    Commercial,
    Defend,
    WaterControl,
    Construction
}

public enum InternalAffairsScheduleState
{
    Active,
    Terminated,
    Interrupted,
    Completed
}

public class InternalAffairsScheduleData
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public int OfficerId { get; set; }
    public InternalAffairsJobType JobType { get; set; }
    public int RemainingMonths { get; set; }
    public int TotalMonths { get; set; }
    public int StartedYear { get; set; }
    public int StartedMonth { get; set; }
    public InternalAffairsScheduleState State { get; set; } = InternalAffairsScheduleState.Active;
    public string InterruptedReason { get; set; } = string.Empty;
}
