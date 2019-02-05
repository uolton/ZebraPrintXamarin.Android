using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content.PM;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using LinkOS.Plugin;
using LinkOS.Plugin.Abstractions;
using System;
using ZebraBluetoothSample.Dependencies;

namespace HawkEye
{
    public class PrinterDiscovery: IPrinterDiscovery
    {
        public PrinterDiscovery() { }

        public void CancelDiscovery()
        {
            if (BluetoothAdapter.DefaultAdapter.IsDiscovering)
            {
                BluetoothAdapter.DefaultAdapter.CancelDiscovery();
                System.Diagnostics.Debug.WriteLine("Cancelling Discovery");
            }
        }

        public void FindBluetoothPrinters(IDiscoveryHandler handler,Activity activity)
        {
            try
            {
                const string permission = Manifest.Permission.AccessCoarseLocation;
                if (ContextCompat.CheckSelfPermission(Android.App.Application.Context, permission) == (int)Permission.Granted)
                {
                    BluetoothDiscoverer.Current.FindPrinters(Android.App.Application.Context, handler);
                    return;
                }
                TempHandler = handler;
                //Finally request permissions with the list of permissions and Id
                ActivityCompat.RequestPermissions(activity, PermissionsLocation, RequestLocationId);
            }catch(Exception ex)
            { }
        }
        public static IDiscoveryHandler TempHandler { get; set; }

        public readonly string[] PermissionsLocation =
        {
          Manifest.Permission.AccessCoarseLocation
        };
        public const int RequestLocationId = 0;



        public void FindUSBPrinters(IDiscoveryHandler handler)
        {
            // UsbDiscoverer.Current.FindPrinters(Android.App.Application.Context, handler);
        }

        public void RequestUSBPermission(IDiscoveredPrinterUsb printer)
        {
            //if (!printer.HasPermissionToCommunicate)
            //{
            //    printer.RequestPermission(Android.App.Application.Context);
            //}
        }
    }
}