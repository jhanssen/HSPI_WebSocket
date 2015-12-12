using System;
using System.Reflection;

namespace HSPI_WebSocket2
{
   
    class Program
    {
        // our homeseer connection details - we can get these from the console arguments too
        private const string serverAddress = "127.0.0.1";
        private const int serverPort = 10400;

        static void Main(string[] args)
        {
            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                AppDomainSetup appSetup = new AppDomainSetup()
                {
                    ApplicationName = "WebSocketDomain",
                    ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                    PrivateBinPath = @"bin/WebSocket",
                    ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile
                };
                var currentAssembly = Assembly.GetExecutingAssembly();
                var otherDomain = AppDomain.CreateDomain("WebSocketDomain", null, appSetup);
                var ret = otherDomain.ExecuteAssemblyByName(
                                            currentAssembly.FullName,
                                            //currentAssembly.Evidence,
                                            args);
                Environment.ExitCode = ret;
                return;
            }

            // create an instance of our plugin.
            HSPI myPlugin = new HSPI();

            try
            {
                myPlugin.Connect(serverAddress, serverPort);
            }
            catch (Exception ex)
            {
                Console.WriteLine("  connection to homeseer failed: " + ex.Message);
                return;
            }

            // let the plugin do it's thing, wait until it shuts down or the connection to homeseer fails.
            try
            {
                while (true)
                {
                    // do nothing for a bit
                    System.Threading.Thread.Sleep(200);

                    // test the connection to homeseer
                    if (!myPlugin.Connected)
                    {
                        Console.WriteLine("Connection to homeseer lost, exiting");
                        break;
                    }

                    // test for a shutdown signal
                    else if (myPlugin.Shutdown)
                    {
                        Console.WriteLine("Plugin has been shut down, exiting");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unhandled exception from Plugin: " + ex.Message);
            }
        }
    }
}
