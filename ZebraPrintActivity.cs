using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using LinkOS.Plugin;
using LinkOS.Plugin.Abstractions;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.ObjectModel;
using System.Text;
using ZebraBluetoothSample.Dependencies;

namespace HawkEye
{
    [Activity(Label = "ZebraPrintActivity", MainLauncher = true)]
    public class ZebraPrintActivity : Activity
    {
        #region Properties

        public delegate void PrinterSelectedHandler(IDiscoveredPrinter printer);
        ObservableCollection<IDiscoveredPrinter> printers = new ObservableCollection<IDiscoveredPrinter>();
        protected IDiscoveredPrinter ChoosenPrinter;
        private ListView listView;
        private ProgressBar _ProgressBar;
        private LinearLayout _LayoutMask;

        #endregion

        protected override void OnCreate(Bundle savedInstanceState)
        {
            try
            {
                base.OnCreate(savedInstanceState);
                SetContentView(Resource.Layout.Main);
                FindViewById<Button>(Resource.Id.BtnConnect).Click += Connect;
                FindViewById<Button>(Resource.Id.Print).Click += Print;
                AppCenter.Start("6272f94a-b778-4dae-90dc-abcf3ed51c99", typeof(Analytics), typeof(Crashes));
                listView = FindViewById<ListView>(Resource.Id.listView);
                listView.ItemClick += ListView_ItemClick;
                _LayoutMask = FindViewById<LinearLayout>(Resource.Id.layoutMask);
                _LayoutMask.Visibility = ViewStates.Gone;
                _ProgressBar = FindViewById<ProgressBar>(Resource.Id.pbSpinner);
                _ProgressBar.Visibility = ViewStates.Gone;
            }
            catch (System.Exception ex)
            {
                Analytics.TrackEvent(ex.Message);
                Crashes.TrackError(ex);
            }
        }

        private void ListView_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            try
            {
                IPrinterDiscovery ip = new PrinterDiscovery();
                ip.CancelDiscovery();
                //Object type for printers returned are DiscoveredPrinters, theres an additional type that says USB but is not the target of this project
                //We assign now the printer selected from the list.
                ChoosenPrinter = printers[e.Position] as IDiscoveredPrinter;
                Toast.MakeText(this, "Printer Selected", ToastLength.Long).Show();
            }
            catch (Exception ex)
            { }
        }

        private void Connect(object sender, EventArgs e)
        {
            StartBluetoothDiscovery();
            RunOnUiThread(() =>
            {
                _ProgressBar.Visibility = _LayoutMask.Visibility = ViewStates.Visible;
                Toast.MakeText(this, "Scanning....this may take a few minutes", ToastLength.Long).Show();
            });
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == 0)
            {
                BluetoothDiscoverer.Current.FindPrinters(Android.App.Application.Context, DiscoveryHandler.Current);
            }
        }

        private void Print(object sender, EventArgs e)
        {
            IConnection connection = null;
            try
            {
                connection = ChoosenPrinter.Connection;
                connection.Open();
                IZebraPrinter printer = ZebraPrinterFactory.Current.GetInstance(connection);
                if ((!CheckPrinterLanguage(connection)) || (!PreCheckPrinterStatus(printer)))
                {

                    return;
                }
                sendZplReceipt(connection);
            }
            catch (System.Exception ex)
            {
                // Connection Exceptions and issues are caught here
                Analytics.TrackEvent(ex.Message);
                Crashes.TrackError(ex);
            }
            finally
            {
                connection.Open();
                if ((connection != null) && (connection.IsConnected))
                    connection.Close();

            }
        }


        #region Zebra methods/functions

        //Start searching for printers
        private void StartBluetoothDiscovery()
        {
            IDiscoveryEventHandler bthandler = DiscoveryHandlerFactory.Current.GetInstance();
            bthandler.OnDiscoveryError += DiscoverError;
            bthandler.OnDiscoveryFinished += DiscoveryFinished;
            bthandler.OnFoundPrinter += DiscoveryHandler_OnFoundPrinter;
            IPrinterDiscovery ip = new PrinterDiscovery();
            ip.FindBluetoothPrinters(bthandler, this);
            BluetoothDiscoverer.Current.FindPrinters(Android.App.Application.Context, bthandler);
            // DependencyService.Get<IPrinterDiscovery>().FindBluetoothPrinters(bthandler);
        }

        private void DiscoverError(object sender, string message)
        {
            RunOnUiThread(() =>
            {
                _ProgressBar.Visibility = _LayoutMask.Visibility = ViewStates.Gone;
                Toast.MakeText(this, "Scanning finished-No Devices Found", ToastLength.Long).Show();
            });
        }

        private void DiscoveryFinished(object sender)
        {
            RunOnUiThread(() =>
            {
                _ProgressBar.Visibility = _LayoutMask.Visibility = ViewStates.Gone;
                Toast.MakeText(this, "Scanning finished", ToastLength.Long).Show();
                BlutoothAddressAdapter bt = new BlutoothAddressAdapter(this, printers);
                listView.Adapter = bt;
            });
        }

        private void DiscoveryHandler_OnFoundPrinter(object sender, IDiscoveredPrinter discoveredPrinter)
        {
            RunOnUiThread(() =>
            {
                Toast.MakeText(this, "Printer found", ToastLength.Long).Show();
                if (!printers.Contains(discoveredPrinter))
                {
                    printers.Add(discoveredPrinter);
                    BlutoothAddressAdapter bt = new BlutoothAddressAdapter(this, printers);
                    listView.Adapter = bt;
                }
            });
        }

        //Connect and send to print
        private void PrintLineMode()
        {
            IConnection connection = null;
            try
            {

                connection = ChoosenPrinter.Connection;
                connection.Open();
                IZebraPrinter printer = ZebraPrinterFactory.Current.GetInstance(connection);
                if ((!CheckPrinterLanguage(connection)) || (!PreCheckPrinterStatus(printer)))
                {

                    return;
                }
                sendZplReceipt(connection);
                if (PostPrintCheckStatus(printer))
                {
                    // Debug.WriteLine("Printing process is done");
                }
            }
            catch (System.Exception ex)
            {
                // Connection Exceptions and issues are caught here
                //Debug.WriteLine(ex.Message);
            }
            finally
            {
                connection.Open();
                if ((connection != null) && (connection.IsConnected))
                    connection.Close();

            }
        }


        //Format and construct the body of the printer string
        private void sendZplReceipt(IConnection printerConnection)
        {
            /*
             This routine is provided to you as an example of how to create a variable length label with user specified data.
             The basic flow of the example is as follows

                Header of the label with some variable data
                REMOVED TO TAKE THE EXAMPLE AS SIMPLE AS POSSIBLE Body of the label
                REMOVED TO TAKE THE EXAMPLE AS SIMPLE AS POSSIBLE     Loops thru user content and creates small line items of printed material
                REMOVED TO TAKE THE EXAMPLE AS SIMPLE AS POSSIBLE Footer of the label

             As you can see, there are some variables that the user provides in the header, body and footer, and this routine uses that to build up a proper ZPL string for printing.
             Using this same concept, you can create one label for your receipt header, one for the body and one for the footer. The body receipt will be duplicated as many items as there are in your variable data

             */
            System.String tmpHeader =
/*
 Some basics of ZPL. Find more information here : http://www.zebra.com

 ^XA indicates the beginning of a label
 ^PW sets the width of the label (in dots)
 ^MNN sets the printer in continuous mode (variable length receipts only make sense with variably sized labels)
 ^LL sets the length of the label (we calculate this value at the end of the routine)
 ^LH sets the reference axis for printing. 
    You will notice we change this positioning of the 'Y' axis (length) as we build up the label. Once the positioning is changed, all new fields drawn on the label are rendered as if '0' is the new home position
 ^FO sets the origin of the field relative to Label Home ^LH
 ^A sets font information 
 ^FD is a field description
 ^GB is graphic boxes (or lines)
 ^B sets barcode information
 ^XZ indicates the end of a label
 */

"^XA" +
"^ FO90,200 ^ GFA,7638,7638,38,,::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::g0IFEIFEIF81IF81IFI07FFC0IFC3F9FE7KF8,g0IFEIFEIFE1IFC1IFI07IF0IFE3F9FE7KF8,g0IFEIFEJF1IFE1IFI07IF8JF3F9FF7KF8,:g0IFEIFEJF9JF1IFI07IFCJFBF9FF7KF8,:g0IFEIFEFF7F9JF1IF8007IFCLF9NF8,gG03FCFF80FF7F9FEFF1IF8007FBFCFF7IF9JF87FE,gG03FCFF00FF7F9FEFF1IF8007F9FCFF7IF9JF83F8,gG03FCFF00FF7F9FEFF1IF8007F9FEFF7IF9JF83F8,gG07F8FF00FF7F9FEFF3IF8007F9JF7IF9JF83F8,:gG0FF8FF00JF9JF3IF8007F9FCJFBF9JF83F8,gG0FF0IFCJF1JF3IF8007FBFCJFBF9JF83F8,gG0FF0IFCIFE1IFE3IF8007IFCJF3F9JF83F8,g01FF0IFCIFE1IFE3IF8007IFCJF3F9JF83F8,g01FE0IFCJF1IFC3IFC007IFCIFE3F9JF83F8,g01FE0IFCJF1IFE3FBFC007IF8JF3F9JF83F8,g03FC0IFCJF9IFE3FBFC007IF8JF3F9JF83F8,g03FC0IFCFF7F9JF3FBFC007IF0JFBF9JF83F8,g03FC0FF00FF3F9FEFF3FBFC007FFE0FF7FBF9JF83F8,g07F80FF00FF3F9FEFF7F9FC007F800FF7FBF9JF83F8,g07F80FF00FF3F9FEFF7IFC007F800FF7FBF9JF83F8,:g0FF00FF00FF3F9FEFF7IFC007F800FF7FBF9JF83F8,:Y01FF00FF00FF7F9FEFF7IFC007F800FF7FBF9JF83F8,Y01IFEIFEJF9FEFF7IFE007F800FF7FBF9JF83F8,Y01IFEIFEJF9FEFF7IFE007F800FF7FBF9FEFF83F8,Y01IFEIFEJF1FEJF1FE007F800FF7FBF9FEFF83F8,:Y01IFEIFEJF1FEJF1FE007F800FF7FBF9FE7F83F8,Y01IFEIFEIFE1FEJF0FE007F800FF7FBF9FE7F83F8,Y01IFEIFEIFC1FEJF0FE007F800FF7FBF9FE7F83F8,,:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::^ FS"
+ "\r\n" +
"^FO130,320" + "\r\n" + "^ASN,40,40" + "\r\n" + "^FDAbove is an Image^FS" + "\r\n" +
"^ FO40,40" +
 "^ GB500,1050,2 ^ FS" +
"\r\n" +
"\r\n" +
"^POI^PW700^MNN^LL500^LH50,60" +
"\r\n" +
" FS" +
"\r\n" +
 "^FO50,400" + "\r\n" + "^ASN,15,15" + "\r\n" + "^FDDate :^FS" + "\r\n" +
"^FO350,400" + "\r\n" + "^ASN,15,15" + "\r\n" + "^FDTIME :^FS" + "\r\n" +
"^FO50,450" + "\r\n" + "^ASN,25,25" + "\r\n" + "^FDEGM # :^FS" + "\r\n" +
"^FO50,500" + "\r\n" + "^ASN,25,25" + "\r\n" + "^FDPlayer Name :^FS" +
"^FO50,550" + "\r\n" + "^ASN,25,25" + "\r\n" + "^FDCredit & Club Meter :^FS" +
"\r\n" +
"^ FO50,800 ^ GB400,0,4,^ FS" +
   "^FO50,850" + "\r\n" + "^A0,N,25,30" + "\r\n" + "^FDLocked by :^FS" +
   "\r\n\n\n\n" +
"^FO50,300" + "\r\n\n\n\n\n\n\n\n\n" + "^GB350,5,5,B,0^FS" + "^ XZ";
            DateTime date = DateTime.Now;
            string dateString = date.ToString("MMM dd, yyyy");
            string header = string.Format(tmpHeader, dateString);
            var t = new UTF8Encoding().GetBytes(header);
            printerConnection.Write(t);
        }

        //Check if the printer is not null
        //If it is null means we should select one first
        protected bool CheckPrinter()
        {
            if (ChoosenPrinter == null)
            {
                //Debug.WriteLine("Please Select a printer");
                //SelectPrinter();
                return false;
            }
            return true;
        }


        //More info https://www.zebra.com/content/dam/zebra/manuals/en-us/software/zpl-zbi2-pm-en.pdf
        protected bool CheckPrinterLanguage(IConnection connection)
        {
            if (!connection.IsConnected)
                connection.Open();
            //  Check the current printer language
            byte[] response = connection.SendAndWaitForResponse(new UTF8Encoding().GetBytes("! U1 getvar \"device.languages\"\r\n"), 500, 100);
            string language = Encoding.UTF8.GetString(response, 0, response.Length);
            if (language.Contains("line_print"))
            {
                // Debug.WriteLine("Switching printer to ZPL Control Language.", "Notification");
            }
            // printer is already in zpl mode
            else if (language.Contains("zpl"))
            {
                return true;
            }

            //  Set the printer command languege
            connection.Write(new UTF8Encoding().GetBytes("! U1 setvar \"device.languages\" \"zpl\"\r\n"));
            response = connection.SendAndWaitForResponse(new UTF8Encoding().GetBytes("! U1 getvar \"device.languages\"\r\n"), 500, 100);
            language = Encoding.UTF8.GetString(response, 0, response.Length);
            if (!language.Contains("zpl"))
            {
                //Debug.WriteLine("Printer language not set. Not a ZPL printer.");
                return false;
            }
            return true;
        }


        //Before printing, check current printer status
        protected bool PreCheckPrinterStatus(IZebraPrinter printer)
        {
            // Check the printer status
            IPrinterStatus status = printer.CurrentStatus;
            if (!status.IsReadyToPrint)
            {
                //Debug.WriteLine("Unable to print. Printer is " + status.Status);
                return false;
            }
            return true;
        }


        //Check what happens to the printer after print command was sent
        protected bool PostPrintCheckStatus(IZebraPrinter printer)
        {
            // Check the status again to verify print happened successfully
            IPrinterStatus status = printer.CurrentStatus;
            // Wait while the printer is printing
            while ((status.NumberOfFormatsInReceiveBuffer > 0) && (status.IsReadyToPrint))
            {
                status = printer.CurrentStatus;
            }
            // verify the print didn't have errors like running out of paper
            if (!status.IsReadyToPrint)
            {
                //Debug.WriteLine("Error durring print. Printer is " + status.Status);
                return false;
            }
            return true;
        }

        #endregion
    }

    #region Adapter

    public class BlutoothAddressAdapter : BaseAdapter
    {
        private Context _Context;
        private ObservableCollection<IDiscoveredPrinter> _FloorList;
        public BlutoothAddressAdapter(Context context, ObservableCollection<IDiscoveredPrinter> FloorList)
        {
            this._Context = context;
            _FloorList = FloorList;
        }
        public override int Count
        {
            get
            {
                return _FloorList.Count;
            }
        }

        public override Java.Lang.Object GetItem(int position)
        {
            return position;
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var view = convertView;
            try
            {
                View row = convertView;
                if (row == null)
                {
                    row = LayoutInflater.From(_Context).Inflate(Resource.Layout.listItem_Row, null, false);
                    TextView floorCheck = row.FindViewById<TextView>(Resource.Id.txtAddress);
                    floorCheck.Text = _FloorList[position].Address;
                }
                return row;
            }
            catch (System.Exception ex)
            {
            }
            return view;
        }
    }

    #endregion
}