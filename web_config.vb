Imports System.Text
Imports System.Web
Imports Scheduler
Imports HomeSeerAPI

Public Class web_config
    Inherits clsPageBuilder
    Dim TimerEnabled As Boolean

    Public Sub New(ByVal pagename As String)
        MyBase.New(pagename)
    End Sub

    Public Overrides Function postBackProc(page As String, data As String, user As String, userRights As Integer) As String

        Dim parts As Collections.Specialized.NameValueCollection
        parts = HttpUtility.ParseQueryString(data)

        Dim secure As String = parts("secure")
        Dim port As String = parts("port")
        Dim pem As String = parts("pem")
        If port <> "" Then
            If secure = "" Or pem = "" Then
                secure = "off"
            End If
            hs.WriteLog(IFACE_NAME, port)
            hs.SaveINISetting("WebSocket", "port", port, INIFILE)
            hs.SaveINISetting("WebSocket", "secure", secure, INIFILE)
            hs.SaveINISetting("WebSocket", "pem", pem, INIFILE)
            WebSocket.open(Convert.ToUInt16(port), (secure = "on"), Encoding.ASCII.GetBytes(pem))
        End If

        Return MyBase.postBackProc(page, data, user, userRights)
    End Function

    Public Function GetPagePlugin(ByVal pageName As String, ByVal user As String, ByVal userRights As Integer, ByVal queryString As String) As String
        Dim stb As New StringBuilder
        Dim instancetext As String = ""
        Try

            Me.reset()

            CurrentPage = Me

            ' handle any queries like mode=something
            Dim parts As Collections.Specialized.NameValueCollection = Nothing
            If (queryString <> "") Then
                parts = HttpUtility.ParseQueryString(queryString)
            End If
            If Instance <> "" Then instancetext = " - " & Instance
            stb.Append(hs.GetPageHeader(pageName, "Sample" & instancetext, "", "", True, False))

            stb.Append(clsPageBuilder.DivStart("pluginpage", ""))

            ' a message area for error messages from jquery ajax postback (optional, only needed if using AJAX calls to get data)
            stb.Append(clsPageBuilder.DivStart("errormessage", "class='errormessage'"))
            stb.Append(clsPageBuilder.DivEnd)

            Me.RefreshIntervalMilliSeconds = 3000
            stb.Append(Me.AddAjaxHandlerPost("id=timer", pageName))

            ' specific page starts here

            stb.Append(BuildContent)

            stb.Append(clsPageBuilder.DivEnd)

            ' add the body html to the page
            Me.AddBody(stb.ToString)

            ' return the full page
            Return Me.BuildPage()
        Catch ex As Exception
            'WriteMon("Error", "Building page: " & ex.Message)
            Return "error - " & Err.Description
        End Try
    End Function

    Function BuildContent() As String
        Dim stb As New StringBuilder
        stb.Append(clsPageBuilder.FormStart("form", "form", "Post"))
        stb.Append("<br><br>Port:<br>")
        Dim portValue = hs.GetINISetting("WebSocket", "port", "8080", INIFILE)
        Dim tb As New clsJQuery.jqTextBox("port", "text", portValue, PageName, 40, False)
        tb.editable = True
        stb.Append(tb.Build)
        stb.Append("<br><br>")
        Dim portSecure = hs.GetINISetting("WebSocket", "secure", "off", INIFILE)
        Dim sec As New clsJQuery.jqCheckBox("secure", "Secure", PageName, False, False)
        sec.checked = (portSecure = "on")
        stb.Append(sec.Build)
        stb.Append("<br>")
        Dim pemValue = hs.GetINISetting("WebSocket", "pem", "", INIFILE)
        'fanfuckingtastic
        Dim pem As New String("<textarea name='pem' rows='20' cols='80'>" & pemValue & "</textarea>")
        stb.Append(pem)
        stb.Append("<br><br>")

        Dim but As New clsJQuery.jqButton("save", "Save", PageName, True)
        stb.Append(but.Build)
        stb.Append(clsPageBuilder.FormEnd)
        Return stb.ToString
    End Function

    Sub PostMessage(ByVal sMessage As String)
        Me.divToUpdate.Add("message", sMessage)
        Me.pageCommands.Add("starttimer", "")
        TimerEnabled = True
    End Sub

End Class

