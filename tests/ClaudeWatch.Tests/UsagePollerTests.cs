using ClaudeWatch.Core;
using Xunit;

public class UsagePollerTests
{
    [Fact]
    public async Task Sucesso_publica_Ok()
    {
        UsageSnapshot? published = null;
        var p = new UsagePoller(
            getToken: _ => Task.FromResult(new TokenResult("AT", SnapshotState.Ok)),
            fetch: (_, _) => Task.FromResult(Snapshots.Of(1, 2, 3)),
            publish: s => published = s, log: _ => { });
        await p.TickAsync(default);
        Assert.Equal(SnapshotState.Ok, published!.State);
    }

    [Fact]
    public async Task Sem_token_publica_NoCredential()
    {
        UsageSnapshot? published = null;
        var p = new UsagePoller(_ => Task.FromResult(new TokenResult(null, SnapshotState.NoCredential)),
            (_, _) => throw new InvalidOperationException("não deveria buscar"),
            s => published = s, _ => { });
        await p.TickAsync(default);
        Assert.Equal(SnapshotState.NoCredential, published!.State);
    }

    [Fact]
    public async Task Token_expirado_publica_Stale_sem_buscar()
    {
        UsageSnapshot? published = null;
        var p = new UsagePoller(_ => Task.FromResult(new TokenResult(null, SnapshotState.Stale)),
            (_, _) => throw new InvalidOperationException("não deveria buscar"),
            s => published = s, _ => { });
        await p.TickAsync(default);
        Assert.Equal(SnapshotState.Stale, published!.State);
    }

    [Fact]
    public async Task Falha_preserva_ultimo_snapshot_como_Stale()
    {
        UsageSnapshot? published = null;
        var ok = true;
        var p = new UsagePoller(_ => Task.FromResult(new TokenResult("AT", SnapshotState.Ok)),
            (_, _) => ok ? Task.FromResult(Snapshots.Of(42, 78, 96)) : throw new HttpRequestException(),
            s => published = s, _ => { });
        await p.TickAsync(default);
        ok = false;
        await p.TickAsync(default);
        Assert.Equal(SnapshotState.Stale, published!.State);
        Assert.Equal(96, published.Sonnet.Pct); // dados antigos preservados
    }
}
