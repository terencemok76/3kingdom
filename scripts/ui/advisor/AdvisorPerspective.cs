using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

/// <summary>
/// Turns an officer's authored <c>advice_profile</c>, or their established
/// attributes when it is absent, into a stable decision lens.  It deliberately
/// does not change command outcomes: it changes how the adviser evaluates the
/// same facts and what trade-off they ask the player to make.
/// </summary>
internal static class AdvisorPerspective
{
    public static string Apply(OfficerData? advisor, LocalizationService? localization, string advice, string adviceTopic = "general")
    {
        if (advisor == null || localization == null || string.IsNullOrWhiteSpace(advice))
        {
            return advice;
        }

        var profile = ResolveProfile(advisor);
        // General advisor buttons already supply one complete city comment.  A
        // profile must change a relevant answer, never add a second, unrelated
        // personality paragraph to it.
        if (adviceTopic != "prisoner_recruit")
        {
            return advice;
        }

        return $"{advice}\n{GetViewComment(localization, profile, adviceTopic)}";
    }

    public static string ResolveProfile(OfficerData officer)
    {
        if (!string.IsNullOrWhiteSpace(officer.AdviceProfile))
        {
            return officer.AdviceProfile.Trim().ToLowerInvariant();
        }

        // A deterministic fallback keeps officer_data.json easy to extend: no
        // random personality is assigned when a new officer is introduced.
        if (officer.Intelligence >= 88 && officer.Politics >= 82)
        {
            return "long_view_planner";
        }

        if (officer.Intelligence >= 85 && officer.Ambition >= 75)
        {
            return "opportunistic_tactician";
        }

        if (officer.Politics >= officer.Intelligence + 10)
        {
            return "pragmatic_steward";
        }

        if (officer.Charm >= 85 && officer.Intelligence >= 70)
        {
            return "diplomatic_bridge";
        }

        if (officer.Leadership >= 85 || officer.Strength >= 90)
        {
            return "decisive_commander";
        }

        return officer.Intelligence >= 75 ? "cautious_analyst" : "pragmatic_steward";
    }

    private static string GetViewComment(LocalizationService localization, string profile, string adviceTopic)
    {
        var comment = localization.T(adviceTopic == "prisoner_recruit"
            ? profile switch
            {
                "long_view_planner" => "ui.advice_profile_comment.prisoner_recruit.long_view_planner",
                "opportunistic_tactician" => "ui.advice_profile_comment.prisoner_recruit.opportunistic_tactician",
                "pragmatic_steward" => "ui.advice_profile_comment.prisoner_recruit.pragmatic_steward",
                "diplomatic_bridge" => "ui.advice_profile_comment.prisoner_recruit.diplomatic_bridge",
                "decisive_commander" => "ui.advice_profile_comment.prisoner_recruit.decisive_commander",
                "cautious_analyst" => "ui.advice_profile_comment.prisoner_recruit.cautious_analyst",
                _ => "ui.advice_profile_comment.prisoner_recruit.adaptive"
            }
            : profile switch
            {
                "long_view_planner" => "ui.advice_profile_comment.long_view_planner",
                "opportunistic_tactician" => "ui.advice_profile_comment.opportunistic_tactician",
                "pragmatic_steward" => "ui.advice_profile_comment.pragmatic_steward",
                "diplomatic_bridge" => "ui.advice_profile_comment.diplomatic_bridge",
                "decisive_commander" => "ui.advice_profile_comment.decisive_commander",
                "cautious_analyst" => "ui.advice_profile_comment.cautious_analyst",
                _ => "ui.advice_profile_comment.adaptive"
            });
        return comment;
    }
}
