using Android.Content;
using Android.Net.Wifi;
using Repositories.Sync;

namespace RealJapanese.Mobile;

public sealed class AndroidSyncNetworkEnvironment : ISyncNetworkEnvironment
{
    public string DeviceName => "Phone " + new string(DeviceInfo.Model.Where(char.IsAsciiLetterOrDigit).Take(32).ToArray());

    public IDisposable EnableDiscovery()
    {
        var wifi = (WifiManager?)Android.App.Application.Context.GetSystemService(Context.WifiService)
            ?? throw new InvalidOperationException("Wi-Fi discovery is unavailable. Use Manual instead.");
        var multicast = wifi.CreateMulticastLock("RealJapanese-sync")
            ?? throw new InvalidOperationException("Wi-Fi discovery is unavailable. Use Manual instead.");
        multicast.SetReferenceCounted(false);
        multicast.Acquire();
        return new Lease(multicast);
    }

    private sealed class Lease(WifiManager.MulticastLock multicast) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (multicast.IsHeld) multicast.Release();
            multicast.Dispose();
        }
    }
}
