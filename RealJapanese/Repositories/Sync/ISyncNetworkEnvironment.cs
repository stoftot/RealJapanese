namespace Repositories.Sync;

public interface ISyncNetworkEnvironment
{
    string DeviceName { get; }
    IDisposable EnableDiscovery();
}

public sealed class DesktopSyncNetworkEnvironment : ISyncNetworkEnvironment
{
    public string DeviceName => "PC " + new string(Environment.MachineName.Where(char.IsAsciiLetterOrDigit).Take(32).ToArray());
    public IDisposable EnableDiscovery() => new DiscoveryLease();
    private sealed class DiscoveryLease : IDisposable { public void Dispose() { } }
}
