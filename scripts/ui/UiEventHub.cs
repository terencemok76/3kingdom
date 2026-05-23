namespace ThreeKingdom.UI;

internal sealed class UiEventHub
{
    internal sealed class OfficerAppointmentsChangedEvent
    {
        public required int OfficerId { get; init; }
        public required int CityId { get; init; }
        public required int FactionId { get; init; }
    }

    public event System.Action<OfficerAppointmentsChangedEvent>? OfficerAppointmentsChanged;

    public void PublishOfficerAppointmentsChanged(int officerId, int cityId, int factionId)
    {
        OfficerAppointmentsChanged?.Invoke(new OfficerAppointmentsChangedEvent
        {
            OfficerId = officerId,
            CityId = cityId,
            FactionId = factionId
        });
    }
}
