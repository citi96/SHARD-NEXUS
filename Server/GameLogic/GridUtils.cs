using System;

namespace Server.GameLogic;

/// <summary>
/// Utility class for grid-based calculations.
/// </summary>
public static class GridUtils
{
    /// <summary>
    /// Calculates the Chebyshev distance between two points on the grid.
    /// </summary>
    public static int ChebyshevDistance(int col1, int row1, int col2, int row2)
        => Math.Max(Math.Abs(col1 - col2), Math.Abs(row1 - row2));

    /// <summary>
    /// Calculates the Chebyshev distance between two units.
    /// </summary>
    public static int ChebyshevDistance(CombatUnit a, CombatUnit b)
        => ChebyshevDistance(a.Col, a.Row, b.Col, b.Row);

    /// <summary>
    /// Calculates the next step toward a target.
    /// </summary>
    public static (int dCol, int dRow) GetStepToward(CombatUnit unit, CombatUnit target)
    {
        int dCol = Math.Sign(target.Col - unit.Col);
        int dRow = Math.Sign(target.Row - unit.Row);
        
        // Prefer Col movement first for visual consistency (unless already on same col)
        if (dCol != 0) return (dCol, 0);
        return (0, dRow);
    }
}
