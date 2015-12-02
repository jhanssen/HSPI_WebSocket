using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HomeSeerAPI;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;

namespace HSPI_WebSocket_CS
{
    public class Homeseer : WebSocketBehavior
    {
        private HomeSeerAPI.IHSApplication app;
        public Homeseer(ref HomeSeerAPI.IHSApplication _app)
        {
            app = _app;
            var en = app.GetDeviceEnumerator();
        }
        protected override void OnMessage(MessageEventArgs e)
        {
            try {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(e.Data);
                String type = obj["type"];
                if (type == "events")
                {
                    var events = app.Event_Info_All();
                    foreach(var ev in events) {
                        app.WriteLog(HSPI_WebSocket.Name, ev.Event_Name);
                    }
                }
                Send(obj["type"]);
            } catch (Exception ex) {
                app.WriteLog(HSPI_WebSocket.Name, ex.ToString());
            }
        }
    }
    public class HSPI_WebSocket
    {
        static public String Name = "WebSocket";
        private HomeSeerAPI.IHSApplication app;
        private WebSocketServer ws = null;
        public HSPI_WebSocket() { }

        public void init(ref HomeSeerAPI.IHSApplication _app)
        {
            app = _app;
            app.WriteLog(Name, "CS init");
        }
        public void open(UInt16 port)
        {
            app.WriteLog(Name, "WS Open port " + port);
            if (!Object.ReferenceEquals(ws, null)) {
                ws.Stop();
            }
            ws = new WebSocketServer(port);
            ws.AddWebSocketService<Homeseer>("/homeseer", () => new Homeseer(ref app) {
                IgnoreExtensions = true
            });
            ws.Start();
        }
        public void onEvent(ref String name, double value, double old, int vref)
        {
            app.WriteLog(Name, "got event " + name + " " + value + " " + vref);
            foreach (var host in ws.WebSocketServices.Hosts) {
                host.Sessions.Broadcast("got event " + name);
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
