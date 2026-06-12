using ClaudeWatch.Core;
using Xunit;

public class BackoffPolicyTests
{
    [Fact]
    public void Sequencia_1_2_5_com_teto_e_reset()
    {
        var normal = TimeSpan.FromSeconds(60);
        var b = new BackoffPolicy();
        Assert.Equal(normal, b.NextDelay(normal));
        b.Failure(); Assert.Equal(TimeSpan.FromMinutes(1), b.NextDelay(normal));
        b.Failure(); Assert.Equal(TimeSpan.FromMinutes(2), b.NextDelay(normal));
        b.Failure(); Assert.Equal(TimeSpan.FromMinutes(5), b.NextDelay(normal));
        b.Failure(); Assert.Equal(TimeSpan.FromMinutes(5), b.NextDelay(normal));
        b.Success(); Assert.Equal(normal, b.NextDelay(normal));
    }
}
