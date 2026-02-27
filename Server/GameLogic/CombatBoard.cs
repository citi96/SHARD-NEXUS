using System.Collections.Generic;
using System.Linq;

namespace Server.GameLogic;

/// <summary>
/// Encapsulates spatial logic and grid-based unit management.
/// </summary>
public class CombatBoard
{
    private readonly List<CombatUnit> _units;
    private const int CombatWidth = CombatConstants.CombatWidth;   // 8
    private const int CombatHeight = CombatConstants.CombatHeight; // 4

    public CombatBoard(List<CombatUnit> units)
    {
        _units = units;
    }

    public List<CombatUnit> GetAllAlive() => _units.Where(u => u.IsAlive).ToList();

    public bool IsOneSideEliminated(out int? winnerTeam)
    {
        bool t0 = _units.Any(u => u.Team == 0 && u.IsAlive);
        bool t1 = _units.Any(u => u.Team == 1 && u.IsAlive);

        if (!t0) { winnerTeam = 1; return true; }
        if (!t1) { winnerTeam = 0; return true; }

        winnerTeam = null;
        return false;
    }

    public HashSet<(int col, int row)> GetOccupiedCells()
    {
        return _units
            .Where(u => u.IsAlive && !u.IsRetreating)
            .Select(u => (u.Col, u.Row))
            .ToHashSet();
    }

    public List<(int col, int row)> GetAdjacentFreeCells(CombatUnit unit)
    {
        var occupied = GetOccupiedCells();
        var result = new List<(int, int)>();
        
        foreach (var (dc, dr) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
        {
            int nc = unit.Col + dc, nr = unit.Row + dr;
            if (nc >= 0 && nc < CombatWidth && nr >= 0 && nr < CombatHeight && !occupied.Contains((nc, nr)))
                result.Add((nc, nr));
        }
        return result;
    }
}
