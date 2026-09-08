using ThreeKingdom.Data;

namespace ThreeKingdom.Core;

public static class CampaignRuntimeContext
{
    public static WorldState? World { get; private set; }
    public static int CampaignId { get; private set; }
    public static bool IsReturningToGameplay { get; private set; }

    public static void Launch(WorldState world, int campaignId)
    {
        World = world;
        CampaignId = campaignId;
        IsReturningToGameplay = false;
    }

    public static ActiveBattleCampaignData? GetActiveCampaign() =>
        World?.ActiveBattleCampaigns.Find(campaign => campaign.Id == CampaignId);

    public static void ReturnToGameplay()
    {
        IsReturningToGameplay = World != null;
        CampaignId = 0;
    }

    public static bool TryConsumeReturningWorld(out WorldState world)
    {
        if (!IsReturningToGameplay || World == null)
        {
            world = null!;
            return false;
        }

        world = World;
        IsReturningToGameplay = false;
        return true;
    }

    public static void Clear()
    {
        World = null;
        CampaignId = 0;
        IsReturningToGameplay = false;
    }
}
