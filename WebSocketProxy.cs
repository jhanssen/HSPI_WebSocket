using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                Dictionary<string, JToken> ret = null;
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
                    try {
                        return ret["value"].ToObject<T>();
                    } catch (Exception ex)
                    {
                        Console.WriteLine("WebSocket: Exception " + ex.ToString());
                    }
                    return default(T);
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

        private string uiFromJqDropList(string sUnique, IPlugInAPI.strTrigActInfo TrigInfo, JObject obj)
        {
            JToken jitems, jpostback, jname, jdescr;
            if (obj.TryGetValue("items", out jitems) && obj.TryGetValue("postback", out jpostback) 
                && obj.TryGetValue("name", out jname) && obj.TryGetValue("description", out jdescr))
            {
                if (jitems.Type == JTokenType.Array && jpostback.Type == JTokenType.Boolean 
                    && jname.Type == JTokenType.String && jdescr.Type == JTokenType.String)
                {
                    var jq = new Scheduler.clsJQuery.jqDropList(jname.ToString() + TrigInfo.UID + sUnique, "Events", true);
                    jq.autoPostBack = jpostback.Value<bool>();
                    foreach (var item in jitems)
                    {
                        if (item.Type == JTokenType.Object)
                        {
                            JToken jvalue, jselected;
                            var itemobj = (JObject)item;
                            if (itemobj.TryGetValue("name", out jname) && itemobj.TryGetValue("value", out jvalue))
                            {
                                var selected = false;
                                if (itemobj.TryGetValue("selected", out jselected))
                                {
                                    if (jselected.Type == JTokenType.Boolean)
                                        selected = jselected.Value<bool>();
                                }
                                jq.AddItem(jname.ToString(), jvalue.ToString(), selected);
                            }
                        }
                    }
                    StringBuilder stb = new StringBuilder();
                    stb.Append(jdescr.ToString());
                    stb.Append(jq.Build());
                    return stb.ToString();
                }
            }
            return "";
        }

        private string uiFromJObject(string sUnique, IPlugInAPI.strTrigActInfo TrigInfo, JObject obj)
        {
            JToken type;
            if (obj.TryGetValue("type", out type))
            {
                if (type.Type == JTokenType.String)
                {
                    string stype = type.ToString();
                    if (stype == "jqDropList")
                    {
                        return uiFromJqDropList(sUnique, TrigInfo, obj);
                    }
                }
            }
            Console.WriteLine("ui from unknown object");
            return "";
        }

        public string triggerBuildUI(string sUnique, IPlugInAPI.strTrigActInfo TrigInfo)
        {
            Console.WriteLine("triggerbuildui 1");
            var ret = waitForPost<JToken>("triggerBuildUI", new List<object>() { getString(TrigInfo.DataIn) });
            if (Object.ReferenceEquals(ret, null))
            {
                Console.WriteLine("null ui");
                return "";
            }
            Console.WriteLine("triggerbuildui 2");
            var token = (JToken)ret;
            if (token.Type == JTokenType.String)
            {
                Console.WriteLine("ui from str");
                return Convert.ToString(ret);
            } else if (token.Type == JTokenType.Object)
            {
                return uiFromJObject(sUnique, TrigInfo, (JObject)token);
            }
            Console.WriteLine("no ui");
            return "";
        }

        public string triggerFormatUI(IPlugInAPI.strTrigActInfo TrigInfo)
        {
            Console.WriteLine("triggerformatui 1");
            return waitForPost<string>("triggerFormatUI", new List<object>() { getString(TrigInfo.DataIn) });
        }

        public IPlugInAPI.strMultiReturn triggerProcessPostUI(NameValueCollection PostData, IPlugInAPI.strTrigActInfo TrigInfoIN)
        {
            Console.WriteLine("triggerprocesspost 1");
            var ret = new IPlugInAPI.strMultiReturn();
            ret.sResult = "";
            ret.DataOut = TrigInfoIN.DataIn;
            ret.TrigActInfo = TrigInfoIN;
            if (Object.ReferenceEquals(PostData, null))
                return ret;
            Console.WriteLine("triggerprocesspost 2");
            if (PostData.Count == 0)
                return ret;
            Console.WriteLine("triggerprocesspost 3");
            var postdict = new Dictionary<string, string>();
            foreach (var k in PostData.Keys)
            {
                if (Object.ReferenceEquals(k, null))
                    continue;
                postdict[k.ToString()] = PostData[k.ToString()];
            }
            string data = waitForPost<string>("triggerProcessPostUI", new List<object>() { postdict, getString(TrigInfoIN.DataIn) });
            if (Object.ReferenceEquals(data, null)) {
                ret.sResult = "Error getting post post ui";
            } else {
                ret.DataOut = Encoding.UTF8.GetBytes(data);
            }
            return ret;
        }

        public void setIOMulti(List<CAPI.CAPIControl> colSend)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            foreach (var ctrl in colSend)
            {
                var dev = (Scheduler.Classes.DeviceClass)app.GetDeviceByRef(ctrl.Ref);
                values[dev.get_Address(app)] = new { value = ctrl.ControlValue, text = ctrl.ControlString };
            }
            server.sendToAll(new { type = "setDeviceValues", data = values });
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
