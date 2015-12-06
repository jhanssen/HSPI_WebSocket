using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
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

        private void postCommand(String function, List<object> arguments, Server.Proc proc)
        {
            server.sendToAll(++cref, proc, new
            {
                type = "command",
                command = new Command() { function = function, arguments = arguments }
            });
        }

        private T waitForPost<T>(String function, List<object> arguments)
        {
            //Console.WriteLine("tigger count");
            if (!server.hasConnections())
                return default(T);
            Task<T> task = Task.Run(() =>
            {
                Dictionary<string, object> ret = null;
                object locked = new object();
                lock (locked)
                {
                    postCommand(function, arguments, obj => { lock (locked) { ret = obj; Monitor.Pulse(locked); } });
                    while (Object.ReferenceEquals(ret, null))
                    {
                        if (!Monitor.Wait(locked, timeout))
                            break;
                    }
                }
                if (ret.ContainsKey("value"))
                {
                    return (T)Convert.ChangeType(ret["value"], typeof(T));
                }

                app.WriteLog("WebSocketProxy", "no value for " + function + ", returning 0");
                return default(T);
            });
            if (!task.Wait(timeout))
            {
                return default(T);
            }
            T retval = task.Result;
            Console.WriteLine("returning " + retval);
            return retval;
        }

        private T waitForPost<T>(String function)
        {
            return waitForPost<T>(function, null);
        }

        private string getString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return "";
            return Encoding.UTF8.GetString(bytes);
        }

        public WebSocketProxy()
        {
        }

        public int triggerCount
        {
            get
            {
                return waitForPost<int>("triggerCount");
            }
        }

        public bool triggerConfigured(IPlugInAPI.strTrigActInfo TrigInfo)
        {
            return waitForPost<bool>("triggerConfigured", new List<object>() { getString(TrigInfo.DataIn) });
        }

        public string triggerName(int TriggerNumber)
        {
            return waitForPost<string>("triggerName", new List<object>() { TriggerNumber });
        }

        public string triggerBuildUI(IPlugInAPI.strTrigActInfo TrigInfo)
        {
            return waitForPost<string>("triggerBuildUI", new List<object>() { getString(TrigInfo.DataIn) });
        }

        public string triggerFormatUI(IPlugInAPI.strTrigActInfo TrigInfo)
        {
            return waitForPost<string>("triggerFormatUI", new List<object>() { getString(TrigInfo.DataIn) });
        }

        public IPlugInAPI.strMultiReturn triggerProcessPostUI(NameValueCollection PostData, IPlugInAPI.strTrigActInfo TrigInfoIN)
        {
            var ret = new IPlugInAPI.strMultiReturn();
            ret.sResult = "";
            ret.DataOut = TrigInfoIN.DataIn;
            ret.TrigActInfo = TrigInfoIN;
            if (Object.ReferenceEquals(PostData, null))
                return ret;
            if (PostData.Count == 0)
                return ret;
            string data = waitForPost<string>("triggerProcessPostUI", new List<object>() { PostData, getString(TrigInfoIN.DataIn) });
            if (Object.ReferenceEquals(data, null)) {
                ret.sResult = "Error getting post post ui";
            } else {
                ret.DataOut = Encoding.UTF8.GetBytes(data);
            }
            return ret;
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
