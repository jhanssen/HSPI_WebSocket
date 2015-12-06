using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HomeSeerAPI;

namespace HSPI_WebSocket2
{
    class WebSocketProxy
    {
        static int timeout = 1000;
        private HomeSeerAPI.IHSApplication app;
        private Server server = new Server();
        private UInt64 cref = 0;
        class Command
        {
            public string function;
            public List<object> arguments;
        }
        private List<Command> commands = new List<Command>();
        private Dictionary<UInt64, String> received = new Dictionary<UInt64, string>();

        private void postCommand(String function, Server.Proc proc)
        {
            server.sendToAll(++cref, proc, new
            {
                type = "command",
                command = new Command() { function = function }
            });
        }

        public WebSocketProxy()
        {
        }

        public int triggerCount
        {
            get
            {
                //Console.WriteLine("tigger count");
                if (!server.hasConnections())
                    return 0;
                Task<int> task = Task.Run(() =>
                {
                    Dictionary<string, object> ret = null;
                    object locked = new object();
                    lock (locked)
                    {
                        postCommand("triggerCount", obj => { lock (locked) { ret = obj; Monitor.Pulse(locked); } });
                        while (Object.ReferenceEquals(ret, null))
                        {
                            if (!Monitor.Wait(locked, timeout))
                                break;
                        }
                    }
                    if (ret.ContainsKey("value"))
                    {
                        var value = Convert.ToInt32(ret["value"]);
                        return value;
                    }

                    app.WriteLog("WebSocketProxy", "no value for triggerCount, returning 0");
                    return 0;
                });
                if (!task.Wait(timeout))
                {
                    return 0;
                }
                int retval = task.Result;
                Console.WriteLine("returning " + retval);
                return retval;
            }
        }
        public string triggerBuildUI(IPlugInAPI.strTrigActInfo TrigInfo)
        {
            return "";
        }

        public void init(ref HomeSeerAPI.IHSApplication _app)
        {
            app = _app;
            server.init(ref _app);
        }

        public void open(UInt16 port, bool secure, byte[] pem)
        {
            server.open(port, secure, pem);
        }

        public void onConfig(int type, int id, int refno, int dac)
        {
            server.onConfig(type, id, refno, dac);
        }

        public void onEvent(String name, double value, double old, int vref)
        {
            server.onEvent(name, value, old, vref);
        }

        public void shutdown()
        {
            server.shutdown();
        }
    }
}
