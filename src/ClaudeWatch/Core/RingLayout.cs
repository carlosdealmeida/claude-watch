namespace ClaudeWatch.Core;

/// <summary>
/// Anéis concêntricos do skin Anéis: o primeiro limite é o de fora.
/// Com 2 limites (Ultra) os anéis são grossos; com 3 ou 4, mais finos para caberem.
/// </summary>
public static class RingLayout
{
    public const double Size = 152;
    public const double Center = Size / 2;
    public const double Outer = 64;
    public const double Gap = 5;
    public const int Max = 4;

    public static double Thickness(int count) => count <= 2 ? 11 : count == 3 ? 9 : 7;

    public static double Radius(int index, int count) =>
        Outer - index * (Thickness(count) + Gap);
}
