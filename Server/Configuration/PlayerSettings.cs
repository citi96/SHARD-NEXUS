using System.Collections.Generic;

namespace Server.Configuration;

public class PlayerSettings
{
    public int StartingHP { get; set; } = 100;
    public int StartingGold { get; set; } = 3;
    public int MaxGold { get; set; } = 50;
    public int BenchSlots { get; set; } = 9;
    public int BoardSlots { get; set; } = 16; // 4 cols × 4 rows = ally board
    public int BaseGoldPerRound { get; set; } = 5;
    public int MaxInterest { get; set; } = 5;
    public int XpBuyCost { get; set; } = 4;
    public int XpBuyAmount { get; set; } = 4;

    public Dictionary<string, int> XpToLevel { get; set; } = new()
    {
        { "1", 2 }, { "2", 6 }, { "3", 10 }, { "4", 20 },
        { "5", 36 }, { "6", 48 }, { "7", 72 }, { "8", 84 }
    };

    public Dictionary<string, int> AutoXpPerRound { get; set; } = new()
    {
        { "1", 2 }, { "2", 2 }, { "3", 2 }, { "4", 4 },
        { "5", 4 }, { "6", 4 }, { "7", 6 }, { "8", 6 }, { "9", 0 }
    };
}
