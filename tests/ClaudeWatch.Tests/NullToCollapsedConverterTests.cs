using System.Globalization;
using System.Windows;
using ClaudeWatch.Widget;
using Xunit;

public class NullToCollapsedConverterTests
{
    private static object Conv(object? v) =>
        new NullToCollapsedConverter().Convert(v!, typeof(Visibility), null!, CultureInfo.InvariantCulture);

    [Fact]
    public void Nulo_colapsa() =>
        Assert.Equal(Visibility.Collapsed, Conv(null));

    [Fact]
    public void Data_fica_visivel() =>
        Assert.Equal(Visibility.Visible,
            Conv(new DateTimeOffset(2026, 6, 11, 15, 0, 0, TimeSpan.Zero)));
}
