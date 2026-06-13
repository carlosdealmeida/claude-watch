using ClaudeWatch.Core;

namespace ClaudeWatch.Infrastructure;

public sealed class UpdateService(
    Version currentVersion,
    GitHubReleaseClient client,
    Action<UpdateStatus> publish,
    Action<string> log)
{
    public async Task<UpdateStatus> CheckAsync(CancellationToken ct)
    {
        try
        {
            var latest = await client.FetchLatestAsync(ct);
            var status = UpdateChecker.Check(currentVersion, latest?.TagName, latest?.HtmlUrl);
            publish(status);
            return status;
        }
        catch (Exception ex)
        {
            log($"update check: {ex.GetType().Name}: {ex.Message}");
            return UpdateStatus.None;
        }
    }

    public async Task RunAsync(TimeSpan initialDelay, TimeSpan interval, CancellationToken ct)
    {
        try { await Task.Delay(initialDelay, ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            await CheckAsync(ct);
            try { await Task.Delay(interval, ct); } catch (OperationCanceledException) { break; }
        }
    }
}
