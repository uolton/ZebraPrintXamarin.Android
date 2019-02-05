using System;
using Android.App;
using LinkOS.Plugin.Abstractions;

namespace ZebraBluetoothSample.Dependencies
{
    public interface IPrinterDiscovery
    {
        void FindBluetoothPrinters(IDiscoveryHandler handler,Activity activity);
        void FindUSBPrinters(IDiscoveryHandler handler);
        void RequestUSBPermission(IDiscoveredPrinterUsb printer);
        void CancelDiscovery();
    }
}
