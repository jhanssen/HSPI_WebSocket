using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Threading.Tasks;
using HomeSeerAPI;
using Scheduler;

namespace HSPI_WebSocket2
{
    class ConfigPage : Scheduler.PageBuilderAndMenu.clsPageBuilder
    {
        private HSPI plugin;

        public static NameValueCollection ParseQueryString(string s)
        {
            NameValueCollection nvc = new NameValueCollection();

            // remove anything other than query string from url
            if (s.Contains("?"))
            {
                s = s.Substring(s.IndexOf('?') + 1);
            }

            foreach (string vp in Regex.Split(s, "&"))
            {
                string[] singlePair = Regex.Split(vp, "=");
                if (singlePair.Length == 2)
                {
                    nvc.Add(singlePair[0], singlePair[1]);
                }
                else
                {
                    // only one key with no value specified in query string
                    nvc.Add(singlePair[0], string.Empty);
                }
            }

            return nvc;
        }

        public ConfigPage(string name, HSPI p) : base(name)
        {
            plugin = p;
        }

        public string GetPagePlugin(string page, string user, int userRights, string queryString)
        {
            var stb = new StringBuilder();
            var instancetext = "";
            NameValueCollection parts;
            if (queryString != "")
                parts = ParseQueryString(queryString);
            var hs = plugin.hs;
            stb.Append(hs.GetPageHeader(PageName, "Sample" + instancetext, "", "", true, false));

            stb.Append(DivStart("pluginpage", ""));

            // a message area for error messages from jquery ajax postback (optional, only needed if using AJAX calls to get data)
            stb.Append(DivStart("errormessage", "class='errormessage'"));
            stb.Append(DivEnd());

            //RefreshIntervalMilliSeconds = 3000;
            //stb.Append(AddAjaxHandlerPost("id=timer", PageName));

            stb.Append(BuildContent());

            stb.Append(DivEnd());
            AddBody(stb.ToString());

            return BuildPage();
        }

        private string BuildContent()
        {
            var hs = plugin.hs;
            var stb = new StringBuilder();
            var portValue = hs.GetINISetting("WebSocket", "port", "8089", HSPI.INI_FILE);
            var portSecure = hs.GetINISetting("WebSocket", "secure", "off", HSPI.INI_FILE);
            var pemValue = hs.GetINISetting("WebSocket", "pem", "", HSPI.INI_FILE);
            stb.Append(FormStart("form", "form", "Post"));
            stb.Append("<br><br>Port:<br>");
            var tb = new clsJQuery.jqTextBox("port", "text", portValue, PageName, 40, false);
            tb.editable = true;
            stb.Append(tb.Build());
            stb.Append("<br>");
            var sec = new clsJQuery.jqCheckBox("secure", "Secure", PageName, true, false);
            sec.@checked = (portSecure == "on");
            stb.Append(sec.Build());
            stb.Append("<br>");
            stb.Append("<textarea name='pem' rows='20' cols='80'>" + pemValue + "</textarea>");
            stb.Append("<br><br>");
            var save = new clsJQuery.jqButton("save", "Save", PageName, true);
            stb.Append(save.Build());
            stb.Append(FormEnd());
            return stb.ToString();
        }

        public override string postBackProc(string page, string data, string user, int userRights)
        {
            NameValueCollection parts = ParseQueryString(data);
            string port, secure, pem;
            try {
                port = parts["port"];
                secure = parts["secure"];
                pem = parts["pem"];

                if (port is string)
                    plugin.hs.SaveINISetting("WebSocket", "port", port, HSPI.INI_FILE);
                else
                    port = plugin.hs.GetINISetting("WebSocket", "port", "8089", HSPI.INI_FILE);
                if (secure is string)
                    plugin.hs.SaveINISetting("WebSocket", "secure", secure, HSPI.INI_FILE);
                else
                    secure = plugin.hs.GetINISetting("WebSocket", "secure", "off", HSPI.INI_FILE);
                if (pem is string)
                    plugin.hs.SaveINISetting("WebSocket", "pem", pem, HSPI.INI_FILE);
                else
                    pem = plugin.hs.GetINISetting("WebSocket", "pem", "", HSPI.INI_FILE);

                plugin.proxy.open(Convert.ToUInt16(port), (secure == "on"), Encoding.ASCII.GetBytes(pem));
            }
            catch (Exception ex)
            {
                plugin.hs.WriteLog(HSPI.IFACE_NAME, "exception caught in postBackProc: " + ex.ToString());
            }

            return base.postBackProc(page, data, user, userRights);
        }
    }
}
