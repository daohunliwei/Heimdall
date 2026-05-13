using System.Collections.Concurrent;
using System.Text.Json;

namespace Heimdall.Core.Services.Tasks;

public sealed class TaskProgressService
{
    private readonly ConcurrentDictionary<Guid, List<Stream>> _subscribers = new();

    public async Task SubscribeAsync(Guid taskId, Stream output, CancellationToken ct)
    {
        _subscribers.AddOrUpdate(taskId,
            _ => new List<Stream> { output },
            (_, list) => { lock (list) { list.Add(output); } return list; });

        try
        {
            // 保持连接直到取消
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // 客户端断开
        }
        finally
        {
            if (_subscribers.TryGetValue(taskId, out var streams))
            {
                lock (streams) { streams.Remove(output); }
            }
        }
    }

    public async Task PublishProgressAsync(Guid taskId, string phase, int percent, string message)
    {
        var payload = JsonSerializer.Serialize(new { phase, percent, message });
        await SendSseEventAsync(taskId, "progress", payload);
    }

    public async Task PublishCompleteAsync(Guid taskId, object result)
    {
        var payload = JsonSerializer.Serialize(result);
        await SendSseEventAsync(taskId, "complete", payload);
    }

    public async Task PublishErrorAsync(Guid taskId, string error)
    {
        var payload = JsonSerializer.Serialize(new { message = error });
        await SendSseEventAsync(taskId, "error", payload);
    }

    private async Task SendSseEventAsync(Guid taskId, string eventType, string data)
    {
        if (!_subscribers.TryGetValue(taskId, out var streams))
        {
            return;
        }

        var message = $"event: {eventType}\ndata: {data}\n\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);

        List<Stream> snapshot;
        lock (streams)
        {
            snapshot = streams.ToList();
        }

        foreach (var stream in snapshot)
        {
            try
            {
                await stream.WriteAsync(bytes);
                await stream.FlushAsync();
            }
            catch
            {
                lock (streams) { streams.Remove(stream); }
            }
        }
    }
}
