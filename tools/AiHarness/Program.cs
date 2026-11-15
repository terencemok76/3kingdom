using System.IO;
using System.Reflection;
using ThreeKingdom.Battle;
using ThreeKingdom.Core;
using ThreeKingdom.Data;
using ThreeKingdom.Map;
using ThreeKingdom.UI;

namespace AiHarness;

internal static partial class Program
{
    private static readonly List<string> Passes = new();
    private static readonly List<string> Failures = new();

    private static void Main(string[] args)
    {
        if (args.Contains("--succession-refactor-only", StringComparer.OrdinalIgnoreCase))
        {
            RunSpyAssassinationPlayerSuccessionPendingTest();
            RunAttackRulerDeathPlayerSuccessionPendingTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--internal-affairs-planning-only", StringComparer.OrdinalIgnoreCase))
        {
            RunInternalAffairsScheduleTest();
            RunInternalAffairsOfficerLockTest();
            RunAiCityPrefectAppointmentTest();
            RunAiCityPrefectReservedFallbackTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--command-helper-rules-only", StringComparer.OrdinalIgnoreCase))
        {
            RunCommandHelperRulesTests();
            PrintSummary();
            return;
        }
        if (args.Contains("--deployment-draft-only", StringComparer.OrdinalIgnoreCase))
        {
            RunDeploymentDraftTests();
            PrintSummary();
            return;
        }
        if (args.Contains("--save-load-only", StringComparer.OrdinalIgnoreCase))
        {
            RunSaveLoadRoundTripTest();
            RunExpandedSaveSlotsTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--map-mission-road-only", StringComparer.OrdinalIgnoreCase))
        {
            RunMapMissionRoadTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--spy-log-privacy-only", StringComparer.OrdinalIgnoreCase))
        {
            RunSpyLogPrivacyTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--ai-officer-hire-only", StringComparer.OrdinalIgnoreCase))
        {
            RunAiOfficerHireTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--map-missions-only", StringComparer.OrdinalIgnoreCase))
        {
            RunMapMissionPresentationTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--appointment-overview-only", StringComparer.OrdinalIgnoreCase))
        {
            RunAppointmentOverviewTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--battle-ai-engineering-planner-only", StringComparer.OrdinalIgnoreCase))
        {
            RunBattleAiEngineeringPlannerTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--battle-ai-supply-planner-only", StringComparer.OrdinalIgnoreCase))
        {
            RunBattleAiSupplyPlannerTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--battle-ai-siege-planner-only", StringComparer.OrdinalIgnoreCase))
        {
            RunBattleAiSiegePlannerTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--battle-ai-strategy-scoring-only", StringComparer.OrdinalIgnoreCase))
        {
            RunBattleAiStrategyScoringTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--strategist-appointments-only", StringComparer.OrdinalIgnoreCase))
        {
            RunStrategistAppointmentTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--military-combat-bonus-only", StringComparer.OrdinalIgnoreCase))
        {
            RunMilitaryCombatBonusTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--military-appointments-only", StringComparer.OrdinalIgnoreCase))
        {
            RunMilitaryAppointmentTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--covert-officer-only", StringComparer.OrdinalIgnoreCase))
        {
            RunCovertOfficerRulesTest();
            RunCovertOfficerResponseTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--officer-contact-only", StringComparer.OrdinalIgnoreCase))
        {
            RunOfficerContactTest();
            RunMultipleOfficerContactTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--main-city-only", StringComparer.OrdinalIgnoreCase))
        {
            RunMainCityRulesTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--officer-intel-only", StringComparer.OrdinalIgnoreCase))
        {
            RunOfficerIntelSnapshotTest();
            PrintSummary();
            return;
        }
        if (args.Contains("--spy-discord-only", StringComparer.OrdinalIgnoreCase))
        {
            RunSpySowDiscordTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--battle-campaign-only", StringComparer.OrdinalIgnoreCase))
        {
            RunCampaignDeploymentValidationTest();
            RunCampaignRetreatReturnEdgesTest();
            RunCampaignCaptiveSuccessionTest();
            RunBattleCampaignLifecycleTest();
            RunBattleCampaignReinforcementTest();
            RunBattleCampaignBlockedEntranceQueueTest();
            RunFieldReinforcementEntranceSideTest();
            RunDefenseReinforcementRequestTest();
            RunAlliedDefenseReinforcementRequestTest();
            RunPrewarAllianceRequestTest();
            RunAiCampaignReinforcementTest();
            RunPlayerCampaignReinforcementCommandTest();
            RunCampaignRetreatDestinationTest();
            RunBattleCampaignReorganizationTest();
            RunBattleCampaignCityCaptureSettlementTest();
            RunAlliedCampaignSettlementTest();
            RunDefenderPlanOverrideTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--battle-environment-only", StringComparer.OrdinalIgnoreCase))
        {
            RunBattlePeriodSupplyTest();
            RunBattleEnvironmentForecastTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--move-allocation-only", StringComparer.OrdinalIgnoreCase))
        {
            RunMoveTroopAllocationTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--caravan-road-only", StringComparer.OrdinalIgnoreCase))
        {
            RunCaravanRoadRiskTest();
            RunAiCaravanAmbushAutoResolutionTest();
            RunAiMultiCityLogisticsTest();
            RunTestCityOwnerSwitchTest();
            RunLongRouteCaravanTest();
            RunLongRouteInterruptionTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--save-summary-only", StringComparer.OrdinalIgnoreCase))
        {
            RunBattleSaveSlotSummaryTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--attack-resolution-only", StringComparer.OrdinalIgnoreCase))
        {
            RunAttackResolutionTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--resource-depot-only", StringComparer.OrdinalIgnoreCase))
        {
            RunResourceDepotStorageTest();
            RunResourceGatedConstructionTest();
            RunConstructionAndManufacturingScheduleTest();
            RunMarketMonthlySnapshotAndTradeTest();
            RunMonthlyAdvisorRetrospectiveTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--monthly-advisor-only", StringComparer.OrdinalIgnoreCase))
        {
            RunMonthlyAdvisorRetrospectiveTest();
            RunMonthlyAdvisorSpeakerSelectionTest();
            PrintSummary();
            return;
        }

        if (args.Contains("--resource-attack-only", StringComparer.OrdinalIgnoreCase))
        {
            RunAiKnownResourceAttackPriorityTest();
            RunAiSiegeEquipmentAttackTest();
            PrintSummary();
            return;
        }

        // Keep test order stable so regression diffs stay easy to compare across runs.
        RunBattleAiEngineeringPlannerTest();
        RunBattleAiSupplyPlannerTest();
        RunBattleAiSiegePlannerTest();
        RunBattleAiStrategyScoringTest();
        RunBattlePeriodSupplyTest();
        RunBattleEnvironmentForecastTest();
        RunAttackSchedulingTest();
        RunAiKnownResourceAttackPriorityTest();
        RunAiSiegeEquipmentAttackTest();
        RunAiCampaignReinforcementTest();
        RunPlayerCampaignReinforcementCommandTest();
        RunCampaignRetreatDestinationTest();
        RunBattleCampaignCityCaptureSettlementTest();
        RunAlliedCampaignSettlementTest();
        RunAttackAutoBreakPactTest();
        RunDefensePromptEligibilityTest();
        RunDefenseDeploymentAffectsCombatOutcomeTest();
        RunTroopCounterCombatTest();
        RunAttackSuccessFlowTest();
        RunAttackFailureFlowTest();
        RunAttackCancellationFlowTest();
        RunMoveSchedulingTest();
        RunMoveTroopAllocationTest();
        RunCaravanRoadRiskTest();
        RunAiCaravanAmbushAutoResolutionTest();
        RunAiMultiCityLogisticsTest();
        RunTestCityOwnerSwitchTest();
        RunLongRouteCaravanTest();
        RunLongRouteInterruptionTest();
        RunCoreActionsTest();
        RunAiDefensiveDiplomacyTruceTest();
        RunAiSpyReconPriorityTest();
        RunAiSpyAssassinationTargetSelectionTest();
        RunAiDiplomacyGiftTest();
        RunAiDiplomacyAllianceTest();
        RunAiDiplomacyDemandTest();
        RunAiDiplomacyBreakPactTest();
        RunDiplomacyGiftScheduleTest();
        RunDiplomacyDemandScheduleTest();
        RunDiplomacyBreakPactResolutionTest();
        RunSpyAssassinationResolutionTest();
        RunSpyAssassinationDesignatedTargetTest();
        RunSpySowDiscordTest();
        RunSpyAssassinationPlayerSuccessionPendingTest();
        RunSpyAssassinationFactionCollapseTest();
        RunAttackResolutionTest();
        RunAttackRulerDeathPlayerSuccessionPendingTest();
        RunSaveLoadRoundTripTest();
        RunExpandedSaveSlotsTest();
        RunSeasonalGoldTest();
        RunSeasonalFoodTest();
        RunResourceDepotStorageTest();
        RunResourceGatedConstructionTest();
        RunConstructionAndManufacturingScheduleTest();
        RunMarketMonthlySnapshotAndTradeTest();
        RunMonthlyCityEventsTest();
        RunMonthlyCityEventSeasonRulesTest();
        RunMonthlyCityEventConditionRulesTest();
        RunMonthlyCityEventPreventionSeverityTest();
        RunMonthlyCityEventAllCitiesRecordedTest();
        RunUpkeepShortageTest();
        RunOfficerProgressionBuffTest();
        RunSpyAndDiplomacyProgressionSuccessBuffTest();
        RunInternalAffairsScheduleTest();
        RunInternalAffairsOfficerLockTest();
        RunPersonnelBonusTest();
        RunAssignOfficerAppointmentTest();
        RunAssignFactionAdvisorTest();
        RunAiCentralAppointmentAssignmentTest();
        RunAiCityPrefectAppointmentTest();
        RunAiCityPrefectReservedFallbackTest();
        RunFireOfficerTest();
        RunHireOfficerTest();
        RunCivilReliefTest();
        RunCivilInvestigationTest();
        RunCivilInvestigationFindsOfficerTest();
        RunFreeOfficerMovementTest();
        RunMultiMonthSoakTest();

        PrintSummary();
    }

    private static void PrintSummary()
    {
        Console.WriteLine($"AI TEST SUMMARY: PASS={Passes.Count} FAIL={Failures.Count}");
        foreach (var line in Passes)
        {
            Console.WriteLine(line);
        }

        foreach (var line in Failures)
        {
            Console.WriteLine(line);
        }

        Environment.ExitCode = Failures.Count == 0 ? 0 : 1;
    }

}
