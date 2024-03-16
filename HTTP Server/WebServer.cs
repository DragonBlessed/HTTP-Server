using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace HTTP_Server
{
    // Web Server Unleashed!
    public static class WebServer
    {
        private static HttpListener listener;

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
        public static int maxSimulataneousConnections = 20;
        private static Semaphore sem = new Semaphore(maxSimulataneousConnections, maxSimulataneousConnections);

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

            string response = "Hello Browser!";
            byte[] encoded = Encoding.UTF8.GetBytes(response);
            context.Response.ContentLength64 = encoded.Length;
            context.Response.OutputStream.Write(encoded, 0, encoded.Length);
            context.Response.OutputStream.Close();
        }

        // Starts the web server
        public static void Start()
        {
            List<IPAddress> localHostIPs = GetLocalHostIPs();
            HttpListener listener = InitializeListener(localHostIPs);
            Start(listener);
        }
    }



}
