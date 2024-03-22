using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Remoting.Contexts;
using System.Reflection;
using System.IO;
using System.Runtime.CompilerServices;

namespace HTTP_Server
{
    // Web Server Unleashed!
    public static class WebServer
    {
        private static HttpListener listener;
        private static Semaphore sem = new Semaphore(20, 20);
        private static Router router = new Router(); 

        // Returns list of IP addresses assigned to localhost network devices, such as hardwired ethernet, wireless, etc.
        private static List<IPAddress> GetLocalHostIPs()
        {
            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());
            List<IPAddress> ret = host.AddressList.Where(ip => ip.AddressFamily ==
         AddressFamily.InterNetwork).ToList();

            return ret;
        }

        private static HttpListener InitializeListener(List<IPAddress> localHostIPs)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost/");

            // listen to IP address also
            localHostIPs.ForEach(ip =>
            {
                string prefix = $"http://{ip}:80/";
                listener.Prefixes.Add(prefix);
                Console.WriteLine($"Listening on IP: {prefix}");
            });

            return listener;
        }

        // Begin listening to connections on a separate worker thread.
        private static void Start(HttpListener listener)
        {
            listener.Start();
            Task.Run(() => RunServer(listener));
        }

        // Start awaiting for connections, up to the "maxSimultaneousConnections" value.
        private static void RunServer(HttpListener listener)
        {
            while (true)
            {
                sem.WaitOne();
                StartConnectionListener(listener);
            }
        }

        // Await connections
        private static async void StartConnectionListener(HttpListener listener)
        {
            // Wait for a connection. Return to caller while waiting
            HttpListenerContext context = await listener.GetContextAsync();
            // Release the semaphore so that another listener can be immediately started
            sem.Release();
            Log(context.Request);

            string response = "<html><head><meta http-equiv='content-type' content='text/html; charset=utf-8'/> </head>Hello Browser!</html>";
            byte[] encoded = Encoding.UTF8.GetBytes(response);
            context.Response.ContentLength64 = encoded.Length;
            context.Response.OutputStream.Write(encoded, 0, encoded.Length);
            context.Response.OutputStream.Close();
            HttpListenerRequest request = context.Request;
            string url = request.RawUrl;
            int index = url.IndexOf("?");
            string path = index != -1 ? url.Substring(0, index) : url; // Only the path, not any of the parameters
            string verb = request.HttpMethod; // HTTP Methods: get, post, delete, etc.
            string parms = index != -1 ? url.Substring(index + 1) : ""; // Params on the URL itself follow the URL and are separated by a ?
            Dictionary<string, string> kvParams = GetKeyValues(parms); // Extract into key-value entries.

            // Pass info to router
            router.Route(verb, path, kvParams);
        } 
        public static Dictionary<string, string> GetKeyValues(string queryString)
        {
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();

            // Split the query string into individual key-value pairs
            var pairs = queryString.Split('&');

            foreach (var pair in pairs)
            {
                if (!string.IsNullOrEmpty(pair))
                {
                    var keyValue = pair.Split('=');
                    if (keyValue.Length == 2)
                    {
                        string key = Uri.UnescapeDataString(keyValue[0]);
                        string value = Uri.UnescapeDataString(keyValue[1]);
                        keyValuePairs[key] = value; // Add or update the key-value pair in the dictionary
                    }
                }
            }

            return keyValuePairs;
        }
        
        public static string GetWebsitePath()
        {
            // Path of exe
            string path = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(path, "..", "..", "..", "Website");
        }

        // Log requests 
        public static void Log(HttpListenerRequest request)
        {
            string url = request.Url.AbsoluteUri;
            int index = url.IndexOf("http://");
            string result = index != -1 ? url.Substring(index + 7) : url;

            Console.WriteLine($"{request.RemoteEndPoint} {request.HttpMethod} /{result}");
        }


        // Starts the web server
        public static void Start()
        {
            string websitePath = GetWebsitePath();
            router.WebsitePath = websitePath; // Configure router with the website path
            listener = InitializeListener(GetLocalHostIPs());
            listener.Start();
            Task.Run(() => RunServer(listener));
        }

        public class ResponsePacket
        {
            public string Redirect { get; set; }
            public byte[] Data { get; set; }
            public string ContentType { get; set; }
            public Encoding Encoding { get; set; }
        }

        public class ExtensionInfo
        {
            public string ContentType { get; set; }
            public Func<string, string, ExtensionInfo, Task<ResponsePacket>> Loader { get; set; }
        }

        private static async Task<ResponsePacket> ImageLoader(string fullPath, string ext, ExtensionInfo extInfo)
        {
            byte[] data = await File.ReadAllBytesAsync(fullPath).ConfigureAwait(false);
            return new ResponsePacket { Data = data, ContentType = extInfo.ContentType };
        }

        private static async Task<ResponsePacket> FileLoader(string fullPath, string ext, ExtensionInfo extInfo)
        {
            string text = await File.ReadAllTextAsync(fullPath).ConfigureAwait(false);
            return new ResponsePacket { Data = Encoding.UTF8.GetBytes(text), ContentType = extInfo.ContentType, Encoding = Encoding.UTF8 };
        }

        private static async Task<ResponsePacket> PageLoader(string fullPath, string ext, ExtensionInfo extInfo)
        {
            string websitePath = ""; // Placeholder
            if (string.IsNullOrEmpty(ext))
            {
                fullPath += ".html";
            }

            fullPath = Path.Combine(websitePath, "Pages", fullPath.TrimStart('/').Replace('/', '\\'));
            return await FileLoader(fullPath, "html", extInfo);
        }

        public class Router
        {
            public string WebsitePath { get; set; }
            private Dictionary<string, ExtensionInfo> extFolderMap;

            public Router()
            {
                extFolderMap = new Dictionary<string, ExtensionInfo>()
                {
                    {"ico", new ExtensionInfo { Loader = ImageLoader, ContentType = "image/ico" }},
                    // other content types added soon
                };
            }

            public async Task<ResponsePacket> Route(string verb, string path, Dictionary<string, string> kvParams)
            {
                string ext = Path.GetExtension(path).TrimStart('.').ToLower();
                if (extFolderMap.TryGetValue(ext, out ExtensionInfo extInfo))
                {
                    string fullPath = Path.Combine(WebsitePath, path.TrimStart('/').Replace('/', '\\'));
                    return await extInfo.Loader(fullPath, ext, extInfo);
                }

                return null;
            }
        }
        private static async Task Respond(HttpListenerResponse response, ResponsePacket resp)
        {
            if (resp == null) return; // Handle null response

            response.ContentType = resp.ContentType;
            response.ContentLength64 = resp.Data.Length;
            if (resp.Encoding != null)
            {
                response.ContentEncoding = resp.Encoding;
            }
            response.StatusCode = (int)HttpStatusCode.OK;

            await response.OutputStream.WriteAsync(resp.Data, 0, resp.Data.Length);
            response.OutputStream.Close();
        }

    }
}




