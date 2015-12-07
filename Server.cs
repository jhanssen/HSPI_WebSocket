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
    public class Homeseer : WebSocketBehavior
    {
        private Server ws;
        public Homeseer(Server _ws)
        {
            ws = _ws;
        }
        private void setDeviceValue(int dref, double value)
        {
            CAPI.CAPIControl[] ctrls = ws.app.CAPIGetControl(dref);
            if (Object.ReferenceEquals(ctrls, null))
                return;
            foreach (var ctrl in ctrls)
            {
                //ws.app.WriteLog("hety", value.ToString() + " " + ctrl.ControlType + " " + ctrl.ControlValue);
                var range = ctrl.Range;
                if (Object.ReferenceEquals(range, null))
                {
                    if (ctrl.ControlValue == value)
                    {
                        ws.app.CAPIControlHandler(ctrl);
                        return;
                    }
                }
                else if (value >= range.RangeStart && value <= range.RangeEnd)
                {
                    ws.app.CAPIControlHandler(ctrl);
                    return;
                }
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(e.Data);
                if (!obj.ContainsKey("type"))
                    return;
                if (!obj.ContainsKey("id"))
                    return;
                String type = obj["type"].ToString();
                UInt64 id = Convert.ToUInt64(obj["id"]);
                // find it in callbacks
                Console.WriteLine("checking id " + id);
                if (type == "response" && obj.ContainsKey("data") && ws.procs.ContainsKey(id))
                {
                    Console.WriteLine("found cb, calling");
                    var proc = ws.procs[id];
                    ws.procs.Remove(id);

                    if (obj["data"] is JObject)
                    {
                        var data = (JObject)obj["data"];
                        proc(data.ToObject<Dictionary<string, JToken>>());
                        return;
                    }
                    proc(null);
                    return;
                }
                Console.WriteLine(type);
                if (type == "events")
                {
                    List<object> l = new List<object>();
                    var events = ws.app.Event_Info_All();
                    foreach (var ev in events)
                    {
                        l.Add(new { name = ev.Event_Name, id = ev.Event_Ref, type = ev.Event_Type });
                        //ws.app.WriteLog(Server.Name, ev.Event_Name);
                    }
                    Send(JsonConvert.SerializeObject(new { id = id, type = "events", events = l }));
                }
                else if (type == "devices")
                {
                    Send(JsonConvert.SerializeObject( new { id = id, type = "devices", devices = ws.devices }));
                }
                else if (type == "set")
                {
                    if (obj.ContainsKey("address") && obj.ContainsKey("value"))
                    {
                        String addr = obj["address"].ToString();
                        int dref = ws.app.GetDeviceRef(addr);
                        object value = obj["value"];
                        if (value is double)
                        {
                            setDeviceValue(dref, (double)value);
                        }
                        else if (value is Int64)
                        {
                            setDeviceValue(dref, (Int64)value);
                        }
                        else if (value is string)
                        {
                            var ctrl = ws.app.CAPIGetSingleControl(dref, true, (string)value, false, true);
                            if (!Object.ReferenceEquals(ctrl, null))
                                ws.app.CAPIControlHandler(ctrl);
                        }
                    }
                }
                else if (type == "fire")
                {
                    if (obj.ContainsKey("event"))
                    {
                        object evt = obj["event"];
                        if (evt is Int64)
                        {
                            ws.app.TriggerEventByRef((int)(Int64)evt);
                        }
                        else if (evt is double)
                        {
                            ws.app.TriggerEventByRef((int)(double)evt);
                        }
                        else if (evt is string)
                        {
                            ws.app.TriggerEvent((string)evt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ws.app.WriteLog(Server.Name, ex.ToString());
            }
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
    };
    public class Server
    {
        public delegate void Proc(Dictionary<String, JToken> obj);

        static public String Name = "WebSocket";
        public HomeSeerAPI.IHSApplication app;
        public Dictionary<String, Device> devices;
        internal Dictionary<UInt64, Proc> procs = new Dictionary<UInt64, Proc>();
        private WebSocketServer ws = null;
        public Server() { }

        private void sendToAll(String payload)
        {
            if (Object.ReferenceEquals(ws, null))
                return;
            foreach (var host in ws.WebSocketServices.Hosts)
            {
                host.Sessions.Broadcast(payload);
            }
        }
        public void sendToAll(UInt64 id, Proc proc, object payload)
        {
            procs[id] = proc;
            sendToAll(JsonConvert.SerializeObject(new { id = id, type = "request", data = payload }));
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
                        Dictionary<double, string> values = new Dictionary<double, string>();
                        dev.get_Code(app);
                        int dref = dev.get_Ref(app);
                        VSVGPairs.VSPair[] pairs = app.DeviceVSP_GetAllStatus(dref);
                        if (!Object.ReferenceEquals(pairs, null))
                        {
                            foreach (var pair in pairs)
                            {
                                values[pair.Value] = app.DeviceVSP_GetStatus(dref, pair.Value, ePairStatusControl.Status);
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
                            changed = dev.get_Last_Change(app)
                        };
                        //app.WriteLog(Name, dev.get_Address(app) + " " + dev.get_Name(app));
                    }
                } while (!en.Finished);
            }
            Dictionary<String, object> ret = new Dictionary<string, object>();
            ret["type"] = "devices";
            ret["devices"] = devices;
            sendToAll(JsonConvert.SerializeObject(ret));
        }

        public void init(ref HomeSeerAPI.IHSApplication _app)
        {
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
            ws = new WebSocketServer(port/*, secure*/);
            if (secure)
            {
                //X509Certificate2 cert = new X509Certificate2(pem);
                //ws.SslConfiguration.ServerCertificate = cert;
            }
            ws.AddWebSocketService<Homeseer>("/homeseer", () => new Homeseer(this)
            {
                IgnoreExtensions = true
            });
            ws.Start();
        }
        public void onConfig(int type, int id, int refno, int dac)
        {
            // type 0 is device change, 1 is event change
            if (type == 0)
            {
                queryDevices();
            }
            else if (type == 1)
            {
                List<object> l = new List<object>();
                var events = app.Event_Info_All();
                foreach (var ev in events)
                {
                    l.Add(new { name = ev.Event_Name, id = ev.Event_Ref, type = ev.Event_Type });
                    //ws.app.WriteLog(Server.Name, ev.Event_Name);
                }
                sendToAll(JsonConvert.SerializeObject(new { type = "events", events = l }));
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
