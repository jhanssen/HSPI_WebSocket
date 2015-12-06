using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Specialized;
using HomeSeerAPI;
using HSCF.Communication.Scs.Communication.EndPoints.Tcp;
using HSCF.Communication.ScsServices.Client;

namespace HSPI_WebSocket2
{
    class HSPI : IPlugInAPI
    {
        internal WebSocketProxy proxy = new WebSocketProxy();

        // our homeseer objects
        internal HomeSeerAPI.IHSApplication hs;
        private IScsServiceClient<IHSApplication> hsClient;
        private IScsServiceClient<IAppCallbackAPI> callbackClient;
        private HomeSeerAPI.IAppCallbackAPI callback;
        private ConfigPage configPage;
        private string sConfigPage = "WebSocket2_Config";

        // our plugin identity
        internal string IFACE_NAME = "WebSocket2";
        internal string INSTANCE_NAME = "";
        internal string INI_FILE = "WebSocket2.ini";

        // our plugin status
        internal bool Shutdown = false;

        public HSPI()
        {
            // cant do much here because this class gets loaded and then destroyed by Homeseer during initial discovery & reflection.
            // instead wait to be initialised during the Connect and InitIO methods, called by our console wrapper and homeseer respectively
        }

        #region Non plugin methods - Connection to Homeseer
        public void Connect(string serverAddress, int serverPort)
        {
            // This method is called by our console wrapper at launch time

            // Create our main connection to the homeseer TCP communication framework
            // part 1 - hs object Proxy
            try
            {
                hsClient = ScsServiceClientBuilder.CreateClient<IHSApplication>(new ScsTcpEndPoint(serverAddress, serverPort), this);
                hsClient.Connect();
                hs = hsClient.ServiceProxy;
                double APIVersion = hs.APIVersion;          // just to make sure our connection is valid
            }
            catch (Exception ex)
            {
                throw new Exception("Error connecting homeseer SCS client: " + ex.Message, ex);
            }

            // part 2 - callback object Proxy
            try
            {
                callbackClient = ScsServiceClientBuilder.CreateClient<IAppCallbackAPI>(new ScsTcpEndPoint(serverAddress, serverPort), this);
                callbackClient.Connect();
                callback = callbackClient.ServiceProxy;
                double APIVersion = callback.APIVersion;    // just to make sure our connection is valid
            }
            catch (Exception ex)
            {
                throw new Exception("Error connecting callback SCS client: " + ex.Message, ex);
            }

            // Establish the reverse connection from homeseer back to our plugin
            try
            { 
                hs.Connect(IFACE_NAME, INSTANCE_NAME);
            }
            catch (Exception ex)
            {
                throw new Exception("Error connecting homeseer to our plugin: " + ex.Message, ex);
            }

        }

        public bool Connected
        {
            get
            {
                // Test our SCS client connection.  The console wrapper will call this periodically to check if there is a problem
                return hsClient.CommunicationState == HSCF.Communication.Scs.Communication.CommunicationStates.Connected;
            }
        }
#endregion

        #region Required Plugin Methods - Information & Initialisation
        public string Name
        {
            get
            {
                return IFACE_NAME;
            }
        }

        public string InstanceFriendlyName()
        {
            return INSTANCE_NAME;
        }

        public IPlugInAPI.strInterfaceStatus InterfaceStatus()
        {
            IPlugInAPI.strInterfaceStatus s = new IPlugInAPI.strInterfaceStatus();
            s.intStatus = IPlugInAPI.enumInterfaceStatus.OK;

            return s;
        }

        public int Capabilities()
        {
            return (int)HomeSeerAPI.Enums.eCapabilities.CA_IO;
        }

        public int AccessLevel()
        {
            return 1;
        }

        public bool HSCOMPort
        {
            get
            {
                return false;
            }
        }

        public bool SupportsAddDevice()
        {
            return false;
        }

        public bool SupportsConfigDevice()
        {
            return false;
        }

        public bool SupportsConfigDeviceAll()
        {
            return false;
        }

        public bool SupportsMultipleInstances()
        {
            return false;
        }

        public bool SupportsMultipleInstancesSingleEXE()
        {
            return false;
        }

        public bool RaisesGenericCallbacks()
        {
            return false;
        }

        public void HSEvent(Enums.HSEvent EventType, object[] parms)
        {
            switch (EventType)
            {
                case Enums.HSEvent.VALUE_CHANGE:
                    proxy.onEvent((string)parms[1], (double)parms[2], (double)parms[3], (int)parms[4]);
                    break;
                case Enums.HSEvent.CONFIG_CHANGE:
                    proxy.onConfig((int)parms[1], (int)parms[2], (int)parms[3], (int)parms[4]);
                    break;
            }
            //throw new NotImplementedException();
        }

        public string InitIO(string ioport)
        {
            // initialise everything here, return a blank string only if successful, or an error message

            try {
                hs.RegisterPage(sConfigPage, IFACE_NAME, INSTANCE_NAME);
                var wpd = new WebPageDesc();
                wpd.link = sConfigPage;
                wpd.linktext = "WebSocket Config";
                wpd.page_title = "WebSocket_Config";
                wpd.plugInName = IFACE_NAME;
                wpd.plugInInstance = INSTANCE_NAME;
                callback.RegisterConfigLink(wpd);
                callback.RegisterEventCB(Enums.HSEvent.VALUE_CHANGE, IFACE_NAME, "");
                callback.RegisterEventCB(Enums.HSEvent.CONFIG_CHANGE, IFACE_NAME, "");
            } catch (Exception ex)
            {
                throw new Exception("Error initializing WebSocket2: " + ex.Message, ex);
            }

            configPage = new ConfigPage(sConfigPage, this);
            proxy.init(ref hs);

            string port = hs.GetINISetting("WebSocket", "port", "8089", INI_FILE);
            string secure = hs.GetINISetting("WebSocket", "secure", "off", INI_FILE);
            string pem = hs.GetINISetting("WebSocket", "pem", "", INI_FILE);
            bool isSecure = (secure == "on");
            if (!isSecure)
            {
                pem = "";
            }
            if (pem == "")
            {
                isSecure = false;
            }
            proxy.open(Convert.ToUInt16(port), isSecure, Encoding.ASCII.GetBytes(pem));

            return "";
        }

        public IPlugInAPI.PollResultInfo PollDevice(int dvref)
        {
            // return the value of a device on demand
            IPlugInAPI.PollResultInfo pollResult = new IPlugInAPI.PollResultInfo();
            pollResult.Result = IPlugInAPI.enumPollResult.Device_Not_Found;
            pollResult.Value = 0;

            return pollResult;
        }

        public void SetIOMulti(List<CAPI.CAPIControl> colSend)
        {
            // homeseer will inform us when the one of our devices has changed.  Push that change through to the field.
        }

        public void ShutdownIO()
        {
            // shut everything down here
            proxy.shutdown();

            // let our console wrapper know we are finished
            Shutdown = true;
        }
        #endregion

        #region Required Plugin Methods - Actions, Triggers & Conditions
        public SearchReturn[] Search(string SearchString, bool RegEx)
        {
            return null;
        }

        public bool ActionAdvancedMode
        {
            get
            {
                //throw new NotImplementedException();
                return false;
            }

            set
            {
                //throw new NotImplementedException();
                // do nothing
            }
        }

        public bool HasTriggers
        {
            get
            {
                hs.WriteLog(IFACE_NAME, "HasTriggers");
                return proxy.triggerCount > 0;
            }
        }

        public int TriggerCount
        {
            get
            {
                hs.WriteLog(IFACE_NAME, "TriggerCount");
                return proxy.triggerCount;
            }
        }

        public string ActionBuildUI(string sUnique, IPlugInAPI.strTrigActInfo ActInfo)
        {
            //throw new NotImplementedException();
            return "";
        }

        public bool ActionConfigured(IPlugInAPI.strTrigActInfo ActInfo)
        {
            //throw new NotImplementedException();
            return true;
        }

        public int ActionCount()
        {
            return 0;
        }

        public string ActionFormatUI(IPlugInAPI.strTrigActInfo ActInfo)
        {
            //throw new NotImplementedException();
            return "";
        }

        public IPlugInAPI.strMultiReturn ActionProcessPostUI(NameValueCollection PostData, IPlugInAPI.strTrigActInfo TrigInfoIN)
        {
            //throw new NotImplementedException();
            return new IPlugInAPI.strMultiReturn();
        }

        public bool ActionReferencesDevice(IPlugInAPI.strTrigActInfo ActInfo, int dvRef)
        {
            //throw new NotImplementedException();
            return false;
        }

        public string get_ActionName(int ActionNumber)
        {
            //throw new NotImplementedException();
            return "";
        }

        public bool get_Condition(IPlugInAPI.strTrigActInfo TrigInfo)
        {
            //throw new NotImplementedException();
            return false;
        }

        public bool get_HasConditions(int TriggerNumber)
        {
            //throw new NotImplementedException();
            return false;
        }

        public string TriggerBuildUI(string sUnique, IPlugInAPI.strTrigActInfo TrigInfo)
        {
            //throw new NotImplementedException();
            return proxy.triggerBuildUI(TrigInfo);
        }

        public string TriggerFormatUI(IPlugInAPI.strTrigActInfo TrigInfo)
        {
            //throw new NotImplementedException();
            return proxy.triggerFormatUI(TrigInfo);
        }

        public IPlugInAPI.strMultiReturn TriggerProcessPostUI(NameValueCollection PostData, IPlugInAPI.strTrigActInfo TrigInfoIN)
        {
            //throw new NotImplementedException();
            return proxy.triggerProcessPostUI(PostData, TrigInfoIN);
        }

        public bool TriggerReferencesDevice(IPlugInAPI.strTrigActInfo TrigInfo, int dvRef)
        {
            //throw new NotImplementedException();
            return false;
        }

        public bool TriggerTrue(IPlugInAPI.strTrigActInfo TrigInfo)
        {
            //throw new NotImplementedException();
            return false;
        }

        public int get_SubTriggerCount(int TriggerNumber)
        {
            //throw new NotImplementedException();
            return 0;
        }

        public string get_SubTriggerName(int TriggerNumber, int SubTriggerNumber)
        {
            //throw new NotImplementedException();
            return "";
        }

        public bool get_TriggerConfigured(IPlugInAPI.strTrigActInfo TrigInfo)
        {
            //throw new NotImplementedException();
            return proxy.triggerConfigured(TrigInfo);
        }

        public string get_TriggerName(int TriggerNumber)
        {
            //throw new NotImplementedException();
            return proxy.triggerName(TriggerNumber);
        }

        public bool HandleAction(IPlugInAPI.strTrigActInfo ActInfo)
        {
            //throw new NotImplementedException();
            return false;
        }

        public void set_Condition(IPlugInAPI.strTrigActInfo TrigInfo, bool Value)
        {
            //throw new NotImplementedException();
        }

        public void SpeakIn(int device, string txt, bool w, string host)
        {
            //throw new NotImplementedException();
        }
        #endregion

        #region Required Plugin Methods - Web Interface
        public string GenPage(string link)
        {
            //throw new NotImplementedException();
            return "";
        }

        public string PagePut(string data)
        {
            //throw new NotImplementedException();
            return "";
        }

        public string GetPagePlugin(string page, string user, int userRights, string queryString)
        {
            //throw new NotImplementedException();
            if (page == configPage.PageName)
                return configPage.GetPagePlugin(page, user, userRights, queryString);
            return "";
        }

        public string PostBackProc(string page, string data, string user, int userRights)
        {
            //throw new NotImplementedException();
            if (page == configPage.PageName)
                return configPage.postBackProc(page, data, user, userRights);
            return "";
        }

        public string ConfigDevice(int @ref, string user, int userRights, bool newDevice)
        {
            //throw new NotImplementedException();
            return "";
        }

        public Enums.ConfigDevicePostReturn ConfigDevicePost(int @ref, string data, string user, int userRights)
        {
            //throw new NotImplementedException();
            return Enums.ConfigDevicePostReturn.DoneAndCancel;
        }
        #endregion

        #region Required Plugin Methods - User defined functions
        public object PluginFunction(string procName, object[] parms)
        {
            //throw new NotImplementedException();
            return null;
        }

        public object PluginPropertyGet(string procName, object[] parms)
        {
            //throw new NotImplementedException();
            return null;
        }

        public void PluginPropertySet(string procName, object value)
        {
            //throw new NotImplementedException();
        }
        #endregion
    }
}
