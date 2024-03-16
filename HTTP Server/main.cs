using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HTTP_Server;

namespace ConsoleWebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            WebServer.Start();
            Console.WriteLine("Server is running. Press Enter to stop.");
            Console.ReadLine();
        }
    }
}
