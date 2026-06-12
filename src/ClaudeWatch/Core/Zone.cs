namespace ClaudeWatch.Core;

public enum Zone { Verde, Ambar, Vermelho }

public static class ZoneRules
{
    public static Zone From(int pct) => pct >= 90 ? Zone.Vermelho : pct >= 70 ? Zone.Ambar : Zone.Verde;
    public static int Clamp(int pct) => Math.Clamp(pct, 0, 100);
}

public static class ZoneColors
{
    public const string Verde = "#3FB950";
    public const string Ambar = "#E8A23D";
    public const string Vermelho = "#F85149";
    public static string Hex(Zone z) => z switch
    { Zone.Vermelho => Vermelho, Zone.Ambar => Ambar, _ => Verde };
}
