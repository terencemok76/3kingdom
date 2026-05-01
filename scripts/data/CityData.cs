using System.Collections.Generic;

namespace ThreeKingdom.Data;

public class CityData
{
    private int _troops;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameZhHant { get; set; } = string.Empty;
    public int OwnerFactionId { get; set; }
    public int Gold { get; set; }
    public int Food { get; set; }
    public int Horses { get; set; }
    public bool HasBowWorkshop { get; set; }
    public bool HasSiegeWorkshop { get; set; }
    public int InfantryTroops { get; set; }
    public int SpearmanTroops { get; set; }
    public int CavalryTroops { get; set; }
    public int ArcherTroops { get; set; }
    public int CrossbowTroops { get; set; }
    public int SiegeTroops { get; set; }
    public int Troops
    {
        get
        {
            var total = GetTotalTroops();
            return total > 0 ? total : _troops;
        }
        set
        {
            _troops = value < 0 ? 0 : value;
        }
    }

    // Phase 1 city development attributes (kept with requested naming)
    public int Farm { get; set; }
    public int Commercial { get; set; }
    public int Defense { get; set; }
    public int DisasterPrevention { get; set; }
    public int Loyalty { get; set; }
    public int LastDevelopYear { get; set; } = -1;
    public int LastDevelopMonth { get; set; } = -1;
    public int LastRecruitYear { get; set; } = -1;
    public int LastRecruitMonth { get; set; } = -1;
    public int LastSearchYear { get; set; } = -1;
    public int LastSearchMonth { get; set; } = -1;
    public int LastCivilReliefYear { get; set; } = -1;
    public int LastCivilReliefMonth { get; set; } = -1;

    public List<int> OfficerIds { get; set; } = new();
    public List<int> ConnectedCityIds { get; set; } = new();
    public float MapX { get; set; }
    public float MapY { get; set; }

    public int GetTotalTroops()
    {
        return InfantryTroops + SpearmanTroops + CavalryTroops + ArcherTroops + CrossbowTroops + SiegeTroops;
    }

    public void EnsureTroopTypesInitialized()
    {
        if (GetTotalTroops() > 0 || _troops <= 0)
        {
            SyncLegacyTroops();
            return;
        }

        InfantryTroops = _troops;
        SyncLegacyTroops();
    }

    public void SyncLegacyTroops()
    {
        _troops = GetTotalTroops();
    }

    public int GetTroops(TroopType troopType)
    {
        return troopType switch
        {
            TroopType.Infantry => InfantryTroops,
            TroopType.Spearman => SpearmanTroops,
            TroopType.Cavalry => CavalryTroops,
            TroopType.Archer => ArcherTroops,
            TroopType.Crossbow => CrossbowTroops,
            TroopType.Siege => SiegeTroops,
            _ => 0
        };
    }

    public void AddTroops(TroopType troopType, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        switch (troopType)
        {
            case TroopType.Infantry:
                InfantryTroops += amount;
                break;
            case TroopType.Spearman:
                SpearmanTroops += amount;
                break;
            case TroopType.Cavalry:
                CavalryTroops += amount;
                break;
            case TroopType.Archer:
                ArcherTroops += amount;
                break;
            case TroopType.Crossbow:
                CrossbowTroops += amount;
                break;
            case TroopType.Siege:
                SiegeTroops += amount;
                break;
        }

        SyncLegacyTroops();
    }

    public void RemoveTroops(TroopType troopType, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        switch (troopType)
        {
            case TroopType.Infantry:
                InfantryTroops = System.Math.Max(0, InfantryTroops - amount);
                break;
            case TroopType.Spearman:
                SpearmanTroops = System.Math.Max(0, SpearmanTroops - amount);
                break;
            case TroopType.Cavalry:
                CavalryTroops = System.Math.Max(0, CavalryTroops - amount);
                break;
            case TroopType.Archer:
                ArcherTroops = System.Math.Max(0, ArcherTroops - amount);
                break;
            case TroopType.Crossbow:
                CrossbowTroops = System.Math.Max(0, CrossbowTroops - amount);
                break;
            case TroopType.Siege:
                SiegeTroops = System.Math.Max(0, SiegeTroops - amount);
                break;
        }

        SyncLegacyTroops();
    }

    public void AddTroopAllocation(TroopAllocationData allocation)
    {
        InfantryTroops += allocation.Infantry;
        SpearmanTroops += allocation.Spearman;
        CavalryTroops += allocation.Cavalry;
        ArcherTroops += allocation.Archer;
        CrossbowTroops += allocation.Crossbow;
        SiegeTroops += allocation.Siege;
        SyncLegacyTroops();
    }

    public void RemoveTroopAllocation(TroopAllocationData allocation)
    {
        InfantryTroops = System.Math.Max(0, InfantryTroops - allocation.Infantry);
        SpearmanTroops = System.Math.Max(0, SpearmanTroops - allocation.Spearman);
        CavalryTroops = System.Math.Max(0, CavalryTroops - allocation.Cavalry);
        ArcherTroops = System.Math.Max(0, ArcherTroops - allocation.Archer);
        CrossbowTroops = System.Math.Max(0, CrossbowTroops - allocation.Crossbow);
        SiegeTroops = System.Math.Max(0, SiegeTroops - allocation.Siege);
        SyncLegacyTroops();
    }
}

