using System.Collections.Generic;

namespace Server.Configuration;

public class ShopSettings
{
    public int RefreshCost { get; set; } = 2;
    public int PityThresholdRare { get; set; } = 15;
    public int PityThresholdEpic { get; set; } = 30;
    public int PityThresholdLegendary { get; set; } = 50;
    
    // Key forms like "1", "2-3", "4-6" -> to be parsed at runtime
    public Dictionary<string, ShopProbabilities> ProbabilitiesByLevel { get; set; } = new();
}

public class ShopProbabilities
{
    public int Common { get; set; }
    public int Uncommon { get; set; }
    public int Rare { get; set; }
    public int Epic { get; set; }
    public int Legendary { get; set; }
}
