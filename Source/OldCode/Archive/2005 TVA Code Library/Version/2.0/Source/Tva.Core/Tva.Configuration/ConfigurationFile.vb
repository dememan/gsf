' 04-12-06

Imports System.Xml
Imports System.Configuration
Imports System.Web.Configuration
Imports Tva.Xml.Common
Imports Tva.IO.FilePath

Namespace Configuration

    Public Class ConfigurationFile

        Private m_configuration As System.Configuration.Configuration

        Public Enum Environments As Integer
            Win
            Web
        End Enum

        Public Sub New()
            Me.New("")
        End Sub

        Public Sub New(ByVal filePath As String)
            m_configuration = GetConfiguration(filePath)
            If m_configuration.HasFile() Then
                ValidateConfigurationFile(m_configuration.FilePath())
            Else
                CreateConfigurationFile(m_configuration.FilePath())
            End If
            m_configuration = GetConfiguration(filePath)
        End Sub

        Public ReadOnly Property Environment() As Environments
            Get
                Dim currentEnvironment As Environments = Environments.Win
                If System.Web.HttpContext.Current() IsNot Nothing Then currentEnvironment = Environments.Web
                Return currentEnvironment
            End Get
        End Property

        Public ReadOnly Property Settings() As SettingsSection
            Get
                Return DirectCast(m_configuration.GetSection("settings"), SettingsSection)
            End Get
        End Property

        Public ReadOnly Property AppSettings() As AppSettingsSection
            Get
                Return m_configuration.AppSettings()
            End Get
        End Property

        Public ReadOnly Property ConnectionStrings() As ConnectionStringsSection
            Get
                Return m_configuration.ConnectionStrings()
            End Get
        End Property

        Public ReadOnly Property FilePath() As String
            Get
                Return m_configuration.FilePath()
            End Get
        End Property

        Public Sub Save()

            m_configuration.Save()

        End Sub

        Public Sub SaveAs(ByVal fileName As String)

            m_configuration.SaveAs(fileName)

        End Sub

        Public Function GetConfiguration(ByVal filePath As String) As System.Configuration.Configuration

            Dim configuration As System.Configuration.Configuration = Nothing

            If filePath IsNot Nothing Then
                If filePath = "" OrElse JustFileExtension(filePath) = ".config" Then
                    Select Case Me.Environment()
                        Case Environments.Win
                            If filePath = "" OrElse _
                                    (filePath <> "" AndAlso JustFileExtension(NoFileExtension(filePath)) = ".exe") Then
                                configuration = ConfigurationManager.OpenExeConfiguration(filePath.TrimEnd(".config".ToCharArray()))
                            Else
                                Throw New ArgumentException()
                            End If
                        Case Environments.Web
                            configuration = WebConfigurationManager.OpenWebConfiguration(filePath)
                    End Select
                Else
                    Throw New ArgumentException()
                End If
            Else
                Throw New ArgumentNullException()
            End If

            Return configuration

        End Function

        Private Sub CreateConfigurationFile(ByVal filePath As String)

            Dim xmlText As New XmlTextWriter(filePath, System.Text.Encoding.UTF8)
            xmlText.Indentation = 4
            xmlText.Formatting = Formatting.Indented
            ' Populate the very basic information required in a config file.
            xmlText.WriteStartDocument()
            xmlText.WriteStartElement("configuration")
            xmlText.WriteEndElement()
            xmlText.WriteEndDocument()
            ' Close the config file.
            xmlText.Close()

            ValidateConfigurationFile(filePath)

        End Sub

        Private Sub ValidateConfigurationFile(ByVal filePath As String)

            Dim name As String = "settings"
            Dim type As String = "Tva.Configuration.SettingsSection, Tva.Core"

            Dim xmlDoc As New XmlDocument()
            xmlDoc.Load(filePath)

            If xmlDoc.DocumentElement.SelectNodes("configSections").Count() = 0 Then
                xmlDoc.DocumentElement.InsertBefore(xmlDoc.CreateElement("configSections"), _
                    xmlDoc.DocumentElement.FirstChild())
            End If
            Dim configSectionsNode As XmlNode = xmlDoc.DocumentElement.SelectSingleNode("configSections")
            If configSectionsNode.SelectNodes("section[@name = '" & name & "']").Count() = 0 Then
                Dim node As XmlNode = xmlDoc.CreateElement("section")
                Attribute(node, "name") = name
                Attribute(node, "type") = type
                configSectionsNode.AppendChild(node)
            End If
            xmlDoc.Save(filePath)

        End Sub

#Region "Shared"
        Private Shared m_defaultConfigFile As ConfigurationFile

        Public Shared ReadOnly Property DefaultConfigFile() As ConfigurationFile
            Get
                If m_defaultConfigFile Is Nothing Then m_defaultConfigFile = New ConfigurationFile()
                Return m_defaultConfigFile
            End Get
        End Property

        Public Shared ReadOnly Property CustomConfigFile(ByVal filePath As String) As ConfigurationFile
            Get
                Return New ConfigurationFile(filePath)
            End Get
        End Property
#End Region

    End Class

End Namespace