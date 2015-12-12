﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HomeSeerAPI;

namespace HSPI_WebSocket2
{
    class WebSocketAPI
    {
        public delegate void Response(UInt64 id, Dictionary<string, JToken> obj);
        public delegate void Request(string str);
        public static event Response OnResponse;

        private static IHSApplication app;
        private static Dictionary<String, Device> devices;

        public static void setApplication(IHSApplication a)
        {
            app = a;
        }
        public static void setDevices(Dictionary<string, Device> devs)
        {
            devices = devs;
        }

        public static void onMessage(string msg, Request request)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(msg);
                if (!obj.ContainsKey("type"))
                    return;
                String type = obj["type"].ToString();
                if (!obj.ContainsKey("id"))
                {
                    if (obj.ContainsKey("data") && obj["data"] is JObject)
                    {
                        if (type == "deviceValueSet")
                        {
                            updateDeviceValue((JObject)obj["data"]);
                        }
                        else if (type == "deviceTextSet")
                        {
                            updateDeviceText((JObject)obj["data"]);
                        }
                    }
                    return;
                }
                UInt64 id = Convert.ToUInt64(obj["id"]);
                // find it in callbacks
                Console.WriteLine("checking id " + id);
                if (type == "response" && obj.ContainsKey("data"))// && procs.ContainsKey(id))
                {
                    Console.WriteLine("found cb, calling");
                    //var proc = procs[id];
                    //procs.Remove(id);

                    if (obj["data"] is JObject)
                    {
                        var data = (JObject)obj["data"];
                        OnResponse(id, data.ToObject<Dictionary<string, JToken>>());
                        return;
                    }
                    OnResponse(id, null);
                    return;
                }
                Console.WriteLine(type);
                if (type == "events")
                {
                    request(JsonConvert.SerializeObject(new { id = id, type = "events", events = WebSocketServer.serializeEvents(app) }));
                }
                else if (type == "devices")
                {
                    request(JsonConvert.SerializeObject(new { id = id, type = "devices", devices = devices }));
                }
                else if (type == "addDevices")
                {
                    if (obj.ContainsKey("args") && obj["args"] is JToken)
                    {
                        var newdevs = addDevices((JToken)obj["args"]);
                        request(JsonConvert.SerializeObject(new { id = id, type = "addDevices", devices = newdevs }));
                    }
                }
                else if (type == "set")
                {
                    if (obj.ContainsKey("address") && obj.ContainsKey("value"))
                    {
                        String addr = obj["address"].ToString();
                        int dref = app.GetDeviceRef(addr);
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
                            var ctrl = app.CAPIGetSingleControl(dref, true, (string)value, false, true);
                            if (!Object.ReferenceEquals(ctrl, null))
                                app.CAPIControlHandler(ctrl);
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
                            app.TriggerEventByRef((int)(Int64)evt);
                        }
                        else if (evt is double)
                        {
                            app.TriggerEventByRef((int)(double)evt);
                        }
                        else if (evt is string)
                        {
                            app.TriggerEvent((string)evt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                app.WriteLog(WebSocketServer.Name, ex.ToString());
            }
        }

        private class DeviceEntry
        {
            public string id;
        };
        private static List<DeviceEntry> addDevices(JToken obj)
        {
            if (!(obj.Type == JTokenType.Array))
                return null;
            var devs = (from d in HSPI.devices(app)
                        where d.get_Interface(app) == HSPI.IFACE_NAME
                        select d).ToDictionary(d => d.get_Address(app), d => d);
            Console.WriteLine("about to create devices " + obj.Type);

            List<DeviceEntry> complete = new List<DeviceEntry>();

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
                                complete.Add(new DeviceEntry() { id = devs[id].get_Address(app) });
                            }
                            else
                            {
                                // we don't have this device, create it
                                var created = (Scheduler.Classes.DeviceClass)app.GetDeviceByRef(app.NewDeviceRef(name));
                                var dref = created.get_Ref(app);
                                created.set_Interface(app, HSPI.IFACE_NAME);
                                created.set_InterfaceInstance(app, "");
                                created.set_Device_Type_String(app, type);
                                created.set_Name(app, name);
                                created.set_Location(app, loc);
                                created.set_Location2(app, loc2);
                                created.set_Address(app, addr);
                                created.set_Code(app, code);
                                created.set_Status_Support(app, false);
                                created.MISC_Set(app, Enums.dvMISC.SHOW_VALUES);
                                created.MISC_Set(app, Enums.dvMISC.NO_LOG);

                                app.SaveEventsDevices();

                                app.DeviceVSP_ClearAll(dref, true);
                                app.DeviceVGP_ClearAll(dref, true);

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

                                complete.Add(new DeviceEntry() { id = created.get_Address(app) });
                            }
                        }
                        else
                        {
                            Console.WriteLine("missing dev tokens");
                        }
                    }
                    else
                    {
                        Console.WriteLine("missing dev properties");
                    }
                }
                else
                {
                    Console.WriteLine("device is not object");
                }
            }

            return complete;
        }

        private static void updateDeviceValue(JObject obj)
        {
            JToken jid, jval;
            if (obj.TryGetValue("id", out jid) && obj.TryGetValue("value", out jval))
            {
                if (jid.Type == JTokenType.String && (jval.Type == JTokenType.Float || jval.Type == JTokenType.Integer))
                {
                    double val = jval.Value<double>();
                    string addr = jid.ToString();

                    app.SetDeviceValueByRef(app.GetDeviceRef(addr), val, true);
                }
            }
        }

        private static void updateDeviceText(JObject obj)
        {
            JToken jid, jval;
            if (obj.TryGetValue("id", out jid) && obj.TryGetValue("text", out jval))
            {
                if (jid.Type == JTokenType.String && jval.Type == JTokenType.String)
                {
                    string txt = jval.ToString();
                    string addr = jid.ToString();

                    app.SetDeviceString(app.GetDeviceRef(addr), txt, false);
                }
            }
        }

        private static void setDeviceValue(int dref, double value)
        {
            CAPI.CAPIControl[] ctrls = app.CAPIGetControl(dref);
            if (Object.ReferenceEquals(ctrls, null))
                return;
            foreach (var ctrl in ctrls)
            {
                //app.WriteLog("hety", value.ToString() + " " + ctrl.ControlType + " " + ctrl.ControlValue);
                var range = ctrl.Range;
                if (Object.ReferenceEquals(range, null))
                {
                    if (ctrl.ControlValue == value)
                    {
                        app.CAPIControlHandler(ctrl);
                        return;
                    }
                }
                else if (value >= range.RangeStart && value <= range.RangeEnd)
                {
                    app.CAPIControlHandler(ctrl);
                    return;
                }
            }
        }

        private static string deviceId(string addr, string code)
        {
            return addr + "-" + code;
        }

        private enum StatusControl
        {
            Status = 0x1,
            Control = 0x2
        };
        private enum Use
        {
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
        private enum Render
        {
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

        private static void addDeviceStatus(int dref, JObject status)
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
                }
                else if ((control & (Int64)StatusControl.Status) != 0)
                {
                    pair = new VSVGPairs.VSPair(ePairStatusControl.Control);
                }
                else if ((control & (Int64)StatusControl.Status) != 0)
                {
                    pair = new VSVGPairs.VSPair(ePairStatusControl.Status);
                }
                else
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
                }
                else
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
                }
                else
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
                }
                else
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
                }
                else
                {
                    pair.IncludeValues = true;
                }

                app.DeviceVSP_AddPair(dref, pair);
            }
        }

        private static void addDeviceGraphic(int dref, JObject graphic)
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
                app.DeviceVGP_AddPair(dref, pair);
            }
        }
    }
}