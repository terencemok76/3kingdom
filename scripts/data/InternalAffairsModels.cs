namespace ThreeKingdom.Data;

public enum InternalAffairsJobType
{
    Farm,
    Commercial,
    Defend,
    WaterControl,
    Construction
}

public enum ConstructionProjectType
{
    None = 0,
    BowWorkshop = 1,
    SiegeWorkshop = 2,
    HorsePasture = 3
}

public enum InternalAffairsScheduleState
{
    Active,
    Paused,
    Terminated,
    Interrupted,
    Completed
}

public class InternalAffairsScheduleData
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public int OfficerId { get; set; }
    public bool IsAuthorizedPlan { get; set; }
    public InternalAffairsJobType JobType { get; set; }
    public ConstructionProjectType ConstructionProjectType { get; set; } = ConstructionProjectType.None;
    public int InvestedGold { get; set; }
    public int RemainingMonths { get; set; }
    public int TotalMonths { get; set; }
    public int StartedYear { get; set; }
    public int StartedMonth { get; set; }
    public InternalAffairsScheduleState State { get; set; } = InternalAffairsScheduleState.Active;
    public string InterruptedReason { get; set; } = string.Empty;
    public int SkipExecutionYear { get; set; } = -1;
    public int SkipExecutionMonth { get; set; } = -1;
}
