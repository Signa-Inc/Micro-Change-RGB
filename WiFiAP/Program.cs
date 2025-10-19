using System;
using System.Diagnostics;
using System.Device.Gpio;
using System.Net;
using System.Threading;
using nanoFramework.Networking;
using System.Device.Wifi;

namespace WifiAP
{
    public class Program
    {
        // RGB LED пины
        private const int RedPin = 18;
        private const int GreenPin = 19;
        private const int BluePin = 21;

        private static GpioController gpio;
        private static GpioPin redLed;
        private static GpioPin greenLed;
        private static GpioPin blueLed;

        // Wi-Fi данные
        private const string WifiSSID = "ISLAM";      // Замени на свой SSID
        private const string WifiPassword = "9312133122";  // Замени на свой пароль

        private static WifiAdapter wifiAdapter;

        public static void Main()
        {
            Debug.WriteLine("Starting ESP32 RGB WebServer...");

            InitializeRgbLed();
            ConnectToWifi();

            StartWebServer();

            Thread.Sleep(Timeout.Infinite);
        }

        private static void InitializeRgbLed()
        {
            gpio = new GpioController();
            redLed = gpio.OpenPin(RedPin, PinMode.Output);
            greenLed = gpio.OpenPin(GreenPin, PinMode.Output);
            blueLed = gpio.OpenPin(BluePin, PinMode.Output);

            SetRgbColor(false, true, false);
            Thread.Sleep(1000);
            SetRgbColor(false, false, false);

            Debug.WriteLine("RGB LED initialized.");
        }

        private static void SetRgbColor(bool r, bool g, bool b)
        {
            redLed.Write(r ? PinValue.High : PinValue.Low);
            greenLed.Write(g ? PinValue.High : PinValue.Low);
            blueLed.Write(b ? PinValue.High : PinValue.Low);

            Debug.WriteLine($"LED state -> R:{r} G:{g} B:{b}");
        }

        public static bool SetColorByName(string color)
        {
            if (string.IsNullOrEmpty(color))
                return false;

            color = color.ToLower().Trim();
            Debug.WriteLine($"SetColorByName: {color}");

            switch (color)
            {
                case "red":
                    SetRgbColor(true, false, false);
                    break;
                case "green":
                    SetRgbColor(false, true, false);
                    break;
                case "blue":
                    SetRgbColor(false, false, true);
                    break;
                case "yellow":
                    SetRgbColor(true, true, false);
                    break;
                case "magenta":
                    SetRgbColor(true, false, true);
                    break;
                case "cyan":
                    SetRgbColor(false, true, true);
                    break;
                case "white":
                    SetRgbColor(true, true, true);
                    break;
                case "off":
                    SetRgbColor(false, false, false);
                    break;
                default:
                    return false;
            }

            return true;
        }

        private static void ConnectToWifi()
        {
            try
            {
                wifiAdapter = WifiAdapter.FindAllAdapters()[0];

                wifiAdapter.AvailableNetworksChanged += Wifi_AvailableNetworksChanged;

                Debug.WriteLine($"Connecting to Wi-Fi: {WifiSSID}");
                Thread.Sleep(5000); // ждём, пока адаптер готов
                //wifiAdapter.ScanAsync(); // стартуем сканирование
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Wi-Fi connection failed: {ex.Message}");
            }
        }

        private static void Wifi_AvailableNetworksChanged(WifiAdapter sender, object e)
        {
            WifiNetworkReport report = sender.NetworkReport;

            foreach (var net in report.AvailableNetworks)
            {
                Debug.WriteLine($"Found network: {net.Ssid}, RSSI: {net.NetworkRssiInDecibelMilliwatts}");

                if (net.Ssid == WifiSSID)
                {
                    sender.Disconnect();

                    // Правильный вызов Connect
                    WifiConnectionResult result = sender.Connect(net, WifiReconnectionKind.Automatic, WifiPassword);

                    if (result.ConnectionStatus == WifiConnectionStatus.Success)
                    {
                        Debug.WriteLine($"✅ Connected to Wi-Fi! ESP32 IP:");
                    }
                    else
                    {
                        Debug.WriteLine($"❌ Failed to connect: {result.ConnectionStatus}");
                    }
                }
            }
        }

        private static void StartWebServer()
        {
            Thread serverThread = new Thread(() =>
            {
                try
                {
                    HttpListener listener = new HttpListener("http");
                    listener.Start();
                    Debug.WriteLine("HTTP WebServer started on port 80");

                    while (true)
                    {
                        try
                        {
                            var context = listener.GetContext();
                            if (context != null)
                                ProcessRequest(context);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"WebServer inner error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WebServer failed to start: {ex.Message}");
                }
            });
            serverThread.Start();
        }

        private static void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            Debug.WriteLine($"Incoming request: {request.RawUrl}");

            try
            {
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                string responseText = "";

                string[] parts = request.RawUrl.Split('?');
                string path = parts[0].ToLower();

                if (path == "/setcolor" && parts.Length > 1)
                {
                    string color = null;
                    var parameters = ParseParams(parts[1]);
                    if (HasKey(parameters, "color"))
                        color = (string)parameters["color"];

                    if (!string.IsNullOrEmpty(color))
                    {
                        bool success = SetColorByName(color);
                        responseText = success ? $"Color set to {color}" : $"Unknown color: {color}";
                    }
                    else
                    {
                        responseText = "Color parameter is missing!";
                    }

                    response.ContentType = "text/plain";
                }
                else
                {
                    response.ContentType = "text/html";
                    responseText = "<h1>ESP32 RGB WebServer</h1>" +
                                   "<p>Use /setcolor?color=red|green|blue|yellow|magenta|cyan|white|off</p>";
                }

                byte[] respBytes = System.Text.Encoding.UTF8.GetBytes(responseText);
                response.ContentLength64 = respBytes.Length;
                response.OutputStream.Write(respBytes, 0, respBytes.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessRequest error: {ex.Message}");
            }
            finally
            {
                response.Close();
            }
        }

        public static System.Collections.Hashtable ParseParams(string query)
        {
            var table = new System.Collections.Hashtable();
            string[] pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                string[] nv = pair.Split('=');
                if (nv.Length == 2)
                    table[nv[0]] = nv[1];
            }
            return table;
        }

        public static bool HasKey(System.Collections.Hashtable table, string key)
        {
            foreach (object k in table.Keys)
                if (k.ToString() == key)
                    return true;
            return false;
        }
    }
}
