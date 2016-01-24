using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.CompilerServices;
using HomeSeerAPI;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HSPI_WebSocket2
{
    public class HomeseerBehavior : WebSocketBehavior
    {
        private WebSocketServer ws;
        public HomeseerBehavior(WebSocketServer _ws)
        {
            ws = _ws;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            WebSocketAPI.onMessage(e.Data, (msg) =>
            {
                Send(msg);
            });
        }
        protected override void OnOpen()
        {
        }
    }
    public class Device
    {
        public class Value
        {
            public String text;
            public double value;
        };
        public String name;
        public String location;
        public String location2;
        public String userNote;
        public Value value;
        public Dictionary<double, string> values;
        public DateTime changed;
        public List<Use> uses;
    };
    public class WebSocketServer
    {
        public delegate void Proc(Dictionary<String, JToken> obj);
        private static Dictionary<UInt64, Proc> procs = new Dictionary<UInt64, Proc>();
        private static Dictionary<String, Device> devices;
        static public String Name = "WebSocket";
        public HomeSeerAPI.IHSApplication app;
        private WebSocketSharp.Server.WebSocketServer ws = null;

        public WebSocketServer()
        {
            WebSocketAPI.OnResponse += (id, obj) =>
            {
                if (procs.ContainsKey(id))
                {
                    Proc proc = procs[id];
                    procs.Remove(id);
                    proc(obj);
                }
            };
        }

        public static object serializeEvents(IHSApplication app)
        {
            Dictionary<int, List<object>> flat = new Dictionary<int, List<object>>();
            var groups = app.Event_Group_Info_All();
            foreach (var gr in groups)
            {
                flat[gr.GroupID] = new List<object>();
            }
            var events = app.Event_Info_All();
            foreach (var ev in events)
            {
                flat[ev.GroupID].Add(new { name = ev.Event_Name, id = ev.Event_Ref, type = ev.Event_Type });
                //ws.app.WriteLog(Server.Name, ev.Event_Name);
            }
            List<object> grouped = new List<object>();
            foreach (var gr in groups)
            {
                grouped.Add(new { groupName = gr.GroupName, groupId = gr.GroupID, events = flat[gr.GroupID] });
            }
            return grouped;
        }

        public void sendToAll(object payload)
        {
            string str;
            if (payload is string)
                str = (string)payload;
            else
                str = JsonConvert.SerializeObject(payload);
            if (Object.ReferenceEquals(ws, null))
                return;
            foreach (var host in ws.WebSocketServices.Hosts)
            {
                app.WriteLog("WebSocketServer", "Broadcasting...");
                host.Sessions.Broadcast(str);
            }
        }
        public void sendToAll(UInt64 id, Proc proc, object payload)
        {
            procs[id] = proc;
            sendToAll(new { id = id, type = "request", data = payload });
        }
        public bool hasConnections()
        {
            foreach (var host in ws.WebSocketServices.Hosts)
            {
                if (host.Sessions.Count > 0)
                    return true;
            }
            return false;
        }
        private void queryDevices()
        {
            Scheduler.Classes.clsDeviceEnumeration en = (Scheduler.Classes.clsDeviceEnumeration)app.GetDeviceEnumerator();
            Scheduler.Classes.DeviceClass dev;
            devices = new Dictionary<String, Device>();
            if (!Object.ReferenceEquals(en, null))
            {
                do
                {
                    dev = en.GetNext();
                    if (!Object.ReferenceEquals(dev, null))
                    {
                        List<Use> uses = new List<Use>();
                        Dictionary<double, string> values = new Dictionary<double, string>();
                        dev.get_Code(app);
                        int dref = dev.get_Ref(app);
                        VSVGPairs.VSPair[] pairs = app.DeviceVSP_GetAllStatus(dref);
                        if (!Object.ReferenceEquals(pairs, null))
                        {
                            foreach (var pair in pairs)
                            {
                                values[pair.Value] = app.DeviceVSP_GetStatus(dref, pair.Value, ePairStatusControl.Status);
                                uses.Add(WebSocketAPI.toAPIUse(pair.ControlUse));
                            }
                        }
                        devices[dev.get_Address(app)] = new Device
                        {
                            name = dev.get_Name(app),
                            value = new Device.Value
                            {
                                text = dev.get_devString(app),
                                value = dev.get_devValue(app)
                            },
                            values = values,
                            location = dev.get_Location(app),
                            location2 = dev.get_Location2(app),
                            userNote = dev.get_UserNote(app),
                            changed = dev.get_Last_Change(app),
                            uses = uses
                        };
                        //app.WriteLog(Name, dev.get_Address(app) + " " + dev.get_Name(app));
                    }
                } while (!en.Finished);
            }

            WebSocketAPI.setDevices(devices);

            Dictionary<String, object> ret = new Dictionary<string, object>();
            ret["type"] = "devices";
            ret["devices"] = devices;
            sendToAll(JsonConvert.SerializeObject(ret));
        }

        public void init(ref HomeSeerAPI.IHSApplication _app)
        {
            WebSocketAPI.setApplication(_app);

            app = _app;
            app.WriteLog(Name, "CS init");
            queryDevices();
        }
        public void open(UInt16 port, bool secure, byte[] pem)
        {
            app.WriteLog(Name, "WS Open port " + port + " secure " + secure);
            if (!Object.ReferenceEquals(ws, null))
            {
                if (ws.IsListening)
                    ws.Stop();
            }
            ws = new WebSocketSharp.Server.WebSocketServer(port/*, secure*/);
            if (secure)
            {
                //X509Certificate2 cert = new X509Certificate2(pem);
                //ws.SslConfiguration.ServerCertificate = cert;
            }
            ws.AddWebSocketService<HomeseerBehavior>("/homeseer", () => new HomeseerBehavior(this)
            {
                IgnoreExtensions = true
            });
            ws.Start();
        }
        public void onConfig(int type, int id, int refno, int dac)
        {
            // type 0 is device change, 1 is event change, 2 is event group change
            if (type == 0)
            {
                queryDevices();
            }
            else if (type == 1 || type == 2)
            {
                sendToAll(JsonConvert.SerializeObject(new { type = "events", events = serializeEvents(app) }));
            }
        }
        public void onEvent(String name, double value, double old, int vref)
        {
            int dref = app.GetDeviceRef(name);
            Scheduler.Classes.DeviceClass dev = (Scheduler.Classes.DeviceClass)app.GetDeviceByRef(dref);
            if (!Object.ReferenceEquals(dev, null))
            {
                var text = dev.get_devString(app);
                Device cached = devices[name];
                cached.value.text = text;
                cached.value.value = value;
                object obj = new
                {
                    type = "change",
                    change = new
                    {
                        address = name,
                        value = new
                        {
                            text = dev.get_devString(app),
                            value = value,
                            old = old
                        }
                    }
                };
                app.WriteLog(Name, "onEvent");
                sendToAll(JsonConvert.SerializeObject(obj));
            }
        }
        public void shutdown()
        {
            if (!Object.ReferenceEquals(ws, null))
            {
                ws.Stop();
            }
            if (app as object != null)
                app.WriteLog(Name, "CS shutdown");
        }
    }
}
