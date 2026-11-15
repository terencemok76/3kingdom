using System;
using System.Collections.Generic;
using System.Linq;
using ThreeKingdom.Core;
using ThreeKingdom.Data;

namespace ThreeKingdom.UI;

internal sealed class DeploymentDraft
{
    private readonly Dictionary<int, AttackOfficerDeploymentData> _deployments = new();
    private readonly List<int> _officerOrder = new();
    public IReadOnlyList<int> OfficerOrder => _officerOrder;
    private readonly Dictionary<int, AttackOfficerDeploymentData> _defenderDeployments = new();
    private readonly List<int> _defenderOrder = new();
    private BattleSupportDeploymentData _support = new();
    public BattleSupportDeploymentData Support => _support.Clone();
    private BattleSupportDeploymentData _defenderSupport = new();

    public void Reset()
    {
        _deployments.Clear();
        _officerOrder.Clear();
        _defenderDeployments.Clear();
        _defenderOrder.Clear();
        _support = new();
        _defenderSupport = new();
    }

    public void BeginReinforcement()
    {
        _defenderDeployments.Clear();
        foreach (var pair in _deployments)
            _defenderDeployments[pair.Key] = CloneDeployment(pair.Value);
        _defenderOrder.Clear();
        _defenderOrder.AddRange(_officerOrder);
        _defenderSupport = _support.Clone();
        _deployments.Clear();
        _officerOrder.Clear();
        _support = new();
    }

    public void RestoreDefenders()
    {
        _deployments.Clear();
        foreach (var pair in _defenderDeployments)
            _deployments[pair.Key] = CloneDeployment(pair.Value);
        _officerOrder.Clear();
        _officerOrder.AddRange(_defenderOrder);
        _support = _defenderSupport.Clone();
    }

    public int MaxEngineerTeams(int available) => Math.Max(0, available - BattleSupportRules.GetRequiredEngineerCount(_support));

    public void SetEngineerTeams(int count, int available) =>
        _support.EngineerTeamCount = Math.Clamp(count, 0, MaxEngineerTeams(available));

    public bool TrySetEquipment(BattleEquipmentType equipment, bool selected, int availableEngineers, out int requiredEngineers)
    {
        var proposed = _support.Clone();
        BattleSupportRules.SetEquipmentSelected(proposed, equipment, selected);
        requiredEngineers = BattleSupportRules.GetRequiredEngineerCount(proposed);
        if (selected && requiredEngineers > availableEngineers) return false;
        _support = proposed;
        if (selected) SetEngineerTeams(_support.EngineerTeamCount, availableEngineers);
        return true;
    }

    public static AttackOfficerDeploymentData CloneDeployment(AttackOfficerDeploymentData deployment) => new()
    {
        OfficerId = deployment.OfficerId,
        TroopType = deployment.TroopType,
        TroopCount = deployment.TroopCount,
        SiegeEngineType = deployment.SiegeEngineType
    };

    public bool ContainsOfficer(int officerId) => _deployments.ContainsKey(officerId);

    public AttackOfficerDeploymentData GetDeployment(int officerId) => CloneDeployment(_deployments[officerId]);

    public int TotalTroops => _deployments.Values.Sum(item => item.TroopCount);

    public void SetTroopType(int officerId, TroopType troopType, int available)
    {
        var deployment = _deployments[officerId];
        deployment.TroopType = troopType;
        if (troopType != TroopType.Siege) deployment.SiegeEngineType = SiegeEngineType.None;
        deployment.TroopCount = Math.Clamp(deployment.TroopCount, 0, MaxTroops(officerId, troopType, available));
    }

    public void SetTroopCount(int officerId, int count, int available)
    {
        var deployment = _deployments[officerId];
        deployment.TroopCount = Math.Clamp(count, 0, MaxTroops(officerId, deployment.TroopType, available));
    }

    public List<AttackOfficerDeploymentData> CreateDeployments() => _deployments.Values
        .Where(item => item.TroopCount > 0)
        .Select(item => new AttackOfficerDeploymentData
        {
            OfficerId = item.OfficerId,
            TroopType = item.TroopType,
            TroopCount = item.TroopCount,
            SiegeEngineType = item.TroopType == TroopType.Siege ? item.SiegeEngineType : SiegeEngineType.None
        }).ToList();

    public DeploymentSnapshot CreateSnapshot() => new(CreateDeployments(), Support);

    public static TroopAllocationData BuildTroopAllocation(IEnumerable<AttackOfficerDeploymentData> deployments)
    {
        var allocation = new TroopAllocationData();
        foreach (var deployment in deployments)
        {
            switch (deployment.TroopType)
            {
                case TroopType.Infantry: allocation.Infantry += deployment.TroopCount; break;
                case TroopType.Spearman: allocation.Spearman += deployment.TroopCount; break;
                case TroopType.Cavalry: allocation.Cavalry += deployment.TroopCount; break;
                case TroopType.Archer: allocation.Archer += deployment.TroopCount; break;
                case TroopType.Crossbow: allocation.Crossbow += deployment.TroopCount; break;
                case TroopType.Siege: allocation.Siege += deployment.TroopCount; break;
            }
        }
        return allocation;
    }

    public static SiegeEngineAllocationData BuildSiegeEngineAllocation(BattleSupportDeploymentData support) => new()
    {
        Ram = support.Ram ? 1 : 0,
        Catapult = support.Catapult ? 1 : 0,
        Ladder = support.Ladder ? 1 : 0
    };

    public void SynchronizeSelection(IEnumerable<int> officerIds, TroopType defaultType)
    {
        var selected = officerIds.ToList();
        var selectedSet = selected.ToHashSet();
        foreach (var id in selected)
            if (!_officerOrder.Contains(id)) _officerOrder.Add(id);
        foreach (var id in _deployments.Keys.Where(id => !selectedSet.Contains(id)).ToList())
            _deployments.Remove(id);
        _officerOrder.RemoveAll(id => !selectedSet.Contains(id));
        foreach (var id in _officerOrder)
            if (!_deployments.ContainsKey(id))
                _deployments[id] = new AttackOfficerDeploymentData { OfficerId = id, TroopType = defaultType, TroopCount = 0, SiegeEngineType = SiegeEngineType.None };
    }

    public int MaxTroops(int officerId, TroopType type, int available) =>
        Math.Max(0, available - _deployments.Values.Where(d => d.OfficerId != officerId && d.TroopType == type).Sum(d => d.TroopCount));
}

internal sealed class DeploymentSnapshot
{
    public List<AttackOfficerDeploymentData> Officers { get; }
    public BattleSupportDeploymentData Support { get; }
    public TroopAllocationData Troops { get; }
    public SiegeEngineAllocationData Equipment { get; }

    public DeploymentSnapshot(List<AttackOfficerDeploymentData> officers, BattleSupportDeploymentData support)
    {
        Officers = officers;
        Support = support;
        Troops = DeploymentDraft.BuildTroopAllocation(officers);
        Equipment = DeploymentDraft.BuildSiegeEngineAllocation(support);
    }
}
