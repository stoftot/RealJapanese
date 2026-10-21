namespace AudioStudio;

public sealed class GenerationHost(GenerationService generation) : IHostedService
{
    public Task StartAsync(CancellationToken token) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token) => generation.StopAsync(token);
}
