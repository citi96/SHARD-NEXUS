using System.Collections.Generic;

namespace Server.Configuration;

public class PlayerSettings
{
    public int StartingHP { get; set; } = 100;
    public int StartingGold { get; set; } = 3;
    public int MaxGold { get; set; } = 50;
    public int BenchSlots { get; set; } = 9;
    public int BoardSlots { get; set; } = 28;
    
    // Key: Level, Value: XP required to reach the next level
    public Dictionary<string, int> XpToLevel { get; set; } = new()
    {
        { "1", 2 }, { "2", 4 }, { "3", 8 }, { "4", 14 }, 
        { "5", 24 }, { "6", 36 }, { "7", 50 }, { "8", 72 }, { "9", 84 }
    };
}
