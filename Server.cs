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

        private static string deviceId(string addr, string code)
        {
            return addr + "-" + code;
        }

        private enum StatusControl {
            Status = 0x1,
            Control = 0x2
        };
        private enum Use {
            On = 0,
            Off = 1,
            Dim = 2,
            OnAlternate = 3,
            Play = 4,
            Pause = 5,
            Stop = 6,
            Forward = 7,
            Rewind = 8,
            Repeat = 9,
            Shuffle = 10,
            HeatSetPoint = 11,
            CoolSetPoint = 12,
            ThermModeOff = 13,
            ThermModeHeat = 14,
            ThermModeCool = 15,
            ThermModeAuto = 16,
            DoorLock = 17,
            DoorUnLock = 18
        };
        private enum Render {
            Values = 0,
            SingleTextFromList = 1,
            ListTextFromList = 2,
            Button = 3,
            ValuesRange = 4,
            ValuesRangeSlider = 5,
            TextList = 6,
            TextBoxNumber = 7,
            TextBoxString = 8,
            RadioOption = 9,
            ButtonScript = 10,
            ColorPicker = 11
        };

        private void addDeviceStatus(int dref, JObject status)
        {
            JToken jvalue, jtext, jcontrol, juse, jrender, jincludevalues;
            if (status.TryGetValue("value", out jvalue) && status.TryGetValue("control", out jcontrol))
            {
                if (jcontrol.Type != JTokenType.Integer)
                    return;
                Console.WriteLine("fa 1");
                var control = jcontrol.Value<Int64>();
                VSVGPairs.VSPair pair;
                if ((control & (Int64)(StatusControl.Status | StatusControl.Control)) != 0)
                {
                    pair = new VSVGPairs.VSPair(ePairStatusControl.Both);
                } else if ((control & (Int64)StatusControl.Status) != 0)
                {
                    pair = new VSVGPairs.VSPair(ePairStatusControl.Control);
                } else if ((control & (Int64)StatusControl.Status) != 0)
                {
                    pair = new VSVGPairs.VSPair(ePairStatusControl.Status);
                } else
                {
                    return;
                }
                if (jvalue.Type == JTokenType.Array)
                {
                    // range
                    pair.PairType = VSVGPairs.VSVGPairType.Range;
                    var value = (JArray)jvalue;
                    if (value.Count() != 2)
                        return;
                    Console.WriteLine("fa 2");
                    pair.RangeStart = value[0].Value<double>();
                    Console.WriteLine("fa 3");
                    pair.RangeEnd = value[1].Value<double>();
                    if (status.TryGetValue("text", out jtext))
                    {
                        if (jtext.Type == JTokenType.Object)
                        {
                            JToken jprefix, jsuffix;
                            if (status.TryGetValue("prefix", out jprefix))
                                pair.RangeStatusPrefix = jprefix.ToString();
                            if (status.TryGetValue("suffix", out jsuffix))
                                pair.RangeStatusSuffix = jsuffix.ToString();
                        }
                    }
                } else
                {
                    // single
                    pair.PairType = VSVGPairs.VSVGPairType.SingleValue;
                    Console.WriteLine("fa 4");
                    pair.Value = jvalue.Value<double>();
                    if (status.TryGetValue("text", out jtext))
                    {
                        pair.Status = jtext.ToString();
                    }
                }
                if (status.TryGetValue("use", out juse))
                {
                    Console.WriteLine("fa 5");
                    var use = (Use)Enum.ToObject(typeof(Use), juse.Value<Int64>());
                    switch (use)
                    {
                        case Use.Dim:
                            pair.ControlUse = ePairControlUse._Dim;
                            break;
                        case Use.Off:
                            pair.ControlUse = ePairControlUse._Off;
                            break;
                        case Use.On:
                            pair.ControlUse = ePairControlUse._On;
                            break;
                        case Use.OnAlternate:
                            pair.ControlUse = ePairControlUse._On_Alternate;
                            break;
                        case Use.Play:
                            pair.ControlUse = ePairControlUse._Play;
                            break;
                        case Use.Pause:
                            pair.ControlUse = ePairControlUse._Pause;
                            break;
                        case Use.Repeat:
                            pair.ControlUse = ePairControlUse._Repeat;
                            break;
                        case Use.Shuffle:
                            pair.ControlUse = ePairControlUse._Shuffle;
                            break;
                        case Use.Rewind:
                            pair.ControlUse = ePairControlUse._Rewind;
                            break;
                        case Use.Forward:
                            pair.ControlUse = ePairControlUse._Forward;
                            break;
                        case Use.Stop:
                            pair.ControlUse = ePairControlUse._Stop;
                            break;
                        case Use.ThermModeAuto:
                            pair.ControlUse = ePairControlUse._ThermModeAuto;
                            break;
                        case Use.ThermModeCool:
                            pair.ControlUse = ePairControlUse._ThermModeCool;
                            break;
                        case Use.ThermModeHeat:
                            pair.ControlUse = ePairControlUse._ThermModeHeat;
                            break;
                        case Use.ThermModeOff:
                            pair.ControlUse = ePairControlUse._ThermModeOff;
                            break;
                        case Use.CoolSetPoint:
                            pair.ControlUse = ePairControlUse._CoolSetPoint;
                            break;
                        case Use.HeatSetPoint:
                            pair.ControlUse = ePairControlUse._HeatSetPoint;
                            break;
                        case Use.DoorLock:
                            pair.ControlUse = ePairControlUse._DoorLock;
                            break;
                        case Use.DoorUnLock:
                            pair.ControlUse = ePairControlUse._DoorUnLock;
                            break;
                    }
                } else
                {
                    pair.ControlUse = ePairControlUse.Not_Specified;
                }
                if (status.TryGetValue("render", out jrender))
                {
                    Console.WriteLine("fa 6");
                    var render = (Render)Enum.ToObject(typeof(Render), jrender.Value<Int64>());
                    switch (render)
                    {
                        case Render.Button:
                            pair.Render = Enums.CAPIControlType.Button;
                            break;
                        case Render.ButtonScript:
                            pair.Render = Enums.CAPIControlType.Button_Script;
                            break;
                        case Render.ColorPicker:
                            pair.Render = Enums.CAPIControlType.Color_Picker;
                            break;
                        case Render.ListTextFromList:
                            pair.Render = Enums.CAPIControlType.List_Text_from_List;
                            break;
                        case Render.RadioOption:
                            pair.Render = Enums.CAPIControlType.Radio_Option;
                            break;
                        case Render.SingleTextFromList:
                            pair.Render = Enums.CAPIControlType.Single_Text_from_List;
                            break;
                        case Render.TextBoxNumber:
                            pair.Render = Enums.CAPIControlType.TextBox_Number;
                            break;
                        case Render.TextBoxString:
                            pair.Render = Enums.CAPIControlType.TextBox_String;
                            break;
                        case Render.TextList:
                            pair.Render = Enums.CAPIControlType.TextList;
                            break;
                        case Render.Values:
                            pair.Render = Enums.CAPIControlType.Values;
                            break;
                        case Render.ValuesRange:
                            pair.Render = Enums.CAPIControlType.ValuesRange;
                            break;
                        case Render.ValuesRangeSlider:
                            pair.Render = Enums.CAPIControlType.ValuesRangeSlider;
                            break;
                    }
                } else
                {
                    pair.Render = Enums.CAPIControlType.Not_Specified;
                }
                if (status.TryGetValue("includeValues", out jincludevalues))
                {
                    if (jincludevalues.Type == JTokenType.Boolean)
                    {
                        Console.WriteLine("fa 7");
                        pair.IncludeValues = jincludevalues.Value<bool>();
                    }
                } else
                {
                    pair.IncludeValues = true;
                }

                ws.app.DeviceVSP_AddPair(dref, pair);
            }
        }

        private void addDeviceGraphic(int dref, JObject graphic)
        {
            JToken jvalue, jgraphic;
            if (graphic.TryGetValue("value", out jvalue))
            {
                var pair = new VSVGPairs.VGPair();
                if (jvalue.Type == JTokenType.Array)
                {
                    // range
                    if (jvalue.Count() != 2)
                        return;
                    pair.PairType = VSVGPairs.VSVGPairType.Range;
                    pair.RangeStart = jvalue[0].Value<double>();
                    pair.RangeEnd = jvalue[1].Value<double>();
                }
                else
                {
                    // single
                    pair.PairType = VSVGPairs.VSVGPairType.SingleValue;
                    pair.Set_Value = jvalue.Value<double>();
                }
                if (graphic.TryGetValue("graphic", out jgraphic))
                {
                    if (jgraphic.Type == JTokenType.String)
                    {
                        // handle data-uri here
                        pair.Graphic = jgraphic.ToString();
                    }
                }
                ws.app.DeviceVGP_AddPair(dref, pair);
            }
        }

        private class Device
        {
            public string id;
        };
        private List<Device> addDevices(JToken obj)
        {
            if (!(obj.Type == JTokenType.Array))
                return null;
            var devs = (from d in HSPI.devices(ws.app)
                        where d.get_Interface(ws.app) == HSPI.IFACE_NAME
                        select d).ToDictionary(d => d.get_Address(ws.app), d => d);
            Console.WriteLine("about to create devices " + obj.Type);

            List<Device> complete = new List<Device>();
            
            foreach (var jnewdev in obj)
            {
                Console.WriteLine("processing device");
                if (jnewdev is JObject)
                {
                    JToken jname, jloc, jloc2, jtype, jpairs, jaddress, jcode;
                    var newdev = (JObject)jnewdev;
                    if (newdev.TryGetValue("name", out jname) && newdev.TryGetValue("type", out jtype) 
                        && newdev.TryGetValue("location", out jloc) && newdev.TryGetValue("location2", out jloc2) 
                        && newdev.TryGetValue("pairs", out jpairs) && newdev.TryGetValue("address", out jaddress))
                    {
                        if (jname.Type == JTokenType.String && jtype.Type == JTokenType.String 
                            && jloc.Type == JTokenType.String && jloc2.Type == JTokenType.String 
                            && jpairs.Type == JTokenType.Array && jaddress.Type == JTokenType.String)
                        {
                            var name = jname.ToString();
                            Console.WriteLine("creating dev " + name);
                            var type = jtype.ToString();
                            var loc = jloc.ToString();
                            var loc2 = jloc2.ToString();
                            var addr = "websocket-" + jaddress.ToString();
                            var code = "";
                            if (newdev.TryGetValue("code", out jcode))
                                code = jcode.ToString();
                            var id = deviceId(addr, code);
                            if (devs.ContainsKey(id))
                            {
                                complete.Add(new Device() { id = devs[id].get_Address(ws.app) });
                            } else
                            {
                                // we don't have this device, create it
                                var created = (Scheduler.Classes.DeviceClass)ws.app.GetDeviceByRef(ws.app.NewDeviceRef(name));
                                var dref = created.get_Ref(ws.app);
                                created.set_Interface(ws.app, HSPI.IFACE_NAME);
                                created.set_InterfaceInstance(ws.app, "");
                                created.set_Device_Type_String(ws.app, type);
                                created.set_Name(ws.app, name);
                                created.set_Location(ws.app, loc);
                                created.set_Location2(ws.app, loc2);
                                created.set_Address(ws.app, addr);
                                created.set_Code(ws.app, code);
                                created.set_Status_Support(ws.app, false);
                                created.MISC_Set(ws.app, Enums.dvMISC.SHOW_VALUES);
                                created.MISC_Set(ws.app, Enums.dvMISC.NO_LOG);

                                ws.app.SaveEventsDevices();

                                ws.app.DeviceVSP_ClearAll(dref, true);
                                ws.app.DeviceVGP_ClearAll(dref, true);

                                foreach (var jitem in jpairs)
                                {
                                    if (jitem is JObject)
                                    {
                                        Console.WriteLine("processing pair 1");
                                        var item = (JObject)jitem;
                                        JToken jstatus, jgraphic;
                                        if (item.TryGetValue("status", out jstatus) && item.TryGetValue("graphic", out jgraphic))
                                        {
                                            Console.WriteLine("processing pair 2");
                                            if (jstatus is JObject && jgraphic is JObject)
                                            {
                                                Console.WriteLine("processing pair 3");
                                                addDeviceStatus(dref, (JObject)jstatus);
                                                addDeviceGraphic(dref, (JObject)jgraphic);
                                            }
                                        }
                                    }
                                }

                                complete.Add(new Device() { id = created.get_Address(ws.app) });
                            }
                        } else
                        {
                            Console.WriteLine("missing dev tokens");
                        }
                    } else
                    {
                        Console.WriteLine("missing dev properties");
                    }
                } else
                {
                    Console.WriteLine("device is not object");
                }
            }

            return complete;
        }

        private void updateDeviceValue(JObject obj)
        {
            JToken jid, jval;
            if (obj.TryGetValue("id", out jid) && obj.TryGetValue("value", out jval))
            {
                if (jid.Type == JTokenType.String && (jval.Type == JTokenType.Float || jval.Type == JTokenType.Integer))
                {
                    double val = jval.Value<double>();
                    string addr = jid.ToString();

                    ws.app.SetDeviceValueByRef(ws.app.GetDeviceRef(addr), val, true);
                }
            }
        }

        private void updateDeviceText(JObject obj)
        {
            JToken jid, jval;
            if (obj.TryGetValue("id", out jid) && obj.TryGetValue("text", out jval))
            {
                if (jid.Type == JTokenType.String && jval.Type == JTokenType.String)
                {
                    string txt = jval.ToString();
                    string addr = jid.ToString();

                    ws.app.SetDeviceString(ws.app.GetDeviceRef(addr), txt, false);
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
                String type = obj["type"].ToString();
                if (!obj.ContainsKey("id")) {
                    if (obj.ContainsKey("data") && obj["data"] is JObject)
                    {
                        if (type == "deviceValueSet")
                        {
                            updateDeviceValue((JObject)obj["data"]);
                        } else if (type == "deviceTextSet")
                        {
                            updateDeviceText((JObject)obj["data"]);
                        }
                    }
                    return;
                }
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
                    Dictionary<int, List<object>> flat = new Dictionary<int, List<object>>();
                    var groups = ws.app.Event_Group_Info_All();
                    foreach (var gr in groups)
                    {
                        flat[gr.GroupID] = new List<object>();
                    }
                    var events = ws.app.Event_Info_All();
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
                    Send(JsonConvert.SerializeObject(new { id = id, type = "events", events = grouped }));
                }
                else if (type == "devices")
                {
                    Send(JsonConvert.SerializeObject( new { id = id, type = "devices", devices = ws.devices }));
                }
                else if (type == "addDevices")
                {
                    if (obj.ContainsKey("args") && obj["args"] is JToken)
                    {
                        var newdevs = addDevices((JToken)obj["args"]);
                        Send(JsonConvert.SerializeObject(new { id = id, type = "addDevices", devices = newdevs }));
                    }
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
