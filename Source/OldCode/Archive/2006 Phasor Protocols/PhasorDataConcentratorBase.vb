Imports System.IO
Imports System.Text
Imports System.Data.OleDb
Imports System.Threading
Imports System.Runtime.Serialization.Formatters
Imports System.Runtime.Serialization.Formatters.Soap
Imports TVA.Measurements
Imports TVA.Communication
Imports TVA.Data.Common
Imports TVA.Common
Imports TVA.IO.FilePath
Imports TVA.ErrorManagement
Imports TVA.Text.Common
Imports TVA.Services
Imports PhasorProtocols

Public MustInherit Class PhasorDataConcentratorBase

    Inherits ConcentratorBase
    Implements IServiceComponent

    Public Event StatusMessage(ByVal status As String)

    Private m_name As String
    Private m_configurationFrame As IConfigurationFrame
    Private m_signalReferences As Dictionary(Of MeasurementKey, SignalReference)
    Private m_publishDescriptor As Boolean
    Private m_exceptionLogger As GlobalExceptionLogger
    Private WithEvents m_communicationServer As ICommunicationServer

    Protected Sub New( _
        ByVal communicationServer As ICommunicationServer, _
        ByVal name As String, _
        ByVal framesPerSecond As Integer, _
        ByVal lagTime As Double, _
        ByVal leadTime As Double, _
        ByVal exceptionLogger As GlobalExceptionLogger)

        MyBase.New(framesPerSecond, lagTime, leadTime)

        m_communicationServer = communicationServer
        m_exceptionLogger = exceptionLogger
        m_publishDescriptor = True

        If String.IsNullOrEmpty(name) Then
            m_name = Me.GetType().Name
        Else
            m_name = name
        End If

    End Sub

    Public Overrides Sub Dispose()

        MyBase.Dispose()

        ' Stop concentrator and communications server
        Me.Enabled = False
        If m_communicationServer IsNot Nothing Then m_communicationServer.Stop()

    End Sub

    Public Sub Start()

        ' Start communications server
        m_communicationServer.Start()

        ' Start concentrator
        Me.Enabled = True

    End Sub

    Public Sub Initialize( _
        ByVal connection As OleDbConnection, _
        ByVal pmuFilterSql As String, _
        ByVal nominalFrequency As LineFrequency, _
        ByVal idCode As UInt16)

        Dim signal As SignalReference
        Dim idLabelCellIndex As New Dictionary(Of String, Integer)

        ' Define protocol independent configuration frame based on PMU filter expression
        Dim configurationFrame As New ConfigurationFrame(idCode, DateTime.UtcNow.Ticks, FramesPerSecond)

        'If String.IsNullOrEmpty(pmuFilterSql) Then pmuFilterSql = "SELECT * FROM Pmu WHERE Enabled <> 0"
        If String.IsNullOrEmpty(pmuFilterSql) Then pmuFilterSql = "SELECT * FROM PMUs WHERE IsActive <> 0"

        ' TODO: Will need to allow a way to define digitals and analogs in the ouput stream at some point
        With RetrieveData(pmuFilterSql, connection).Rows
            For x As Integer = 0 To .Count - 1
                With .Item(x)
                    Dim cell As New ConfigurationCell(configurationFrame, Convert.ToUInt16(.Item("ID")), nominalFrequency)

                    ' To allow rectangular phasors and/or scaled values - make adjustments here...
                    cell.PhasorDataFormat = DataFormat.FloatingPoint
                    cell.PhasorCoordinateFormat = CoordinateFormat.Polar
                    cell.FrequencyDataFormat = DataFormat.FloatingPoint
                    cell.AnalogDataFormat = DataFormat.FloatingPoint

                    'cell.IDLabel = TruncateRight(.Item("Acronym").ToString(), cell.IDLabelLength)
                    cell.IDLabel = TruncateRight(.Item("PMUID_Uniq").ToString(), cell.IDLabelLength)

                    'cell.StationName = TruncateRight(.Item("Name").ToString(), cell.MaximumStationNameLength)
                    cell.StationName = TruncateRight(.Item("PMUName").ToString(), cell.MaximumStationNameLength)

                    ' Load all phasors as defined in the database
                    'With RetrieveData(String.Format("SELECT Label, Type FROM Phasor WHERE ID={0} ORDER BY IOIndex", Convert.ToInt32(.Item("ID"))), connection).Rows
                    With RetrieveData(String.Format("SELECT Label, Type FROM Phasors WHERE PMUID='{0}' ORDER BY PhasorIndex", cell.IDLabel), connection).Rows
                        For y As Integer = 0 To .Count - 1
                            With .Item(y)
                                cell.PhasorDefinitions.Add( _
                                    New PhasorDefinition(cell, y, TruncateRight(.Item("Label").ToString(), cell.MaximumStationNameLength), _
                                    1, 0.0F, IIf(.Item("Type").ToString().StartsWith("V", StringComparison.OrdinalIgnoreCase), _
                                    PhasorType.Voltage, PhasorType.Current), Nothing))
                            End With
                        Next
                    End With

                    ' Add frequency definition
                    cell.FrequencyDefinition = New FrequencyDefinition( _
                        cell, String.Format("{0} Frequency", cell.IDLabel), _
                        Convert.ToInt32(.Item("FreqScale")), _
                        Convert.ToSingle(.Item("FreqOffset")), _
                        Convert.ToInt32(.Item("DfDtScale")), _
                        Convert.ToSingle(.Item("DfDtOffset")))

                    configurationFrame.Cells.Add(cell)
                End With
            Next
        End With

        ' Define protocol specific configuration frame - if user doesn't need to broadcast a protocol
        ' specific configuration frame, they can choose to just return protocol independent configuration
        m_configurationFrame = CreateNewConfigurationFrame(configurationFrame)

        ' Cache configuration frame for reference
        UpdateStatus(String.Format("Caching new {0} [{1}] configuration frame...", Name, m_configurationFrame.IDCode))

        Try
            Dim cachePath As String = String.Format("{0}ConfigurationCache\", GetApplicationPath())
            If Not Directory.Exists(cachePath) Then Directory.CreateDirectory(cachePath)
            Dim configFile As FileStream = File.Create(String.Format("{0}{1}.{2}.configuration.xml", cachePath, RemoveWhiteSpace(Name), m_configurationFrame.IDCode))

            With New SoapFormatter
                .AssemblyFormat = FormatterAssemblyStyle.Simple
                .TypeFormat = FormatterTypeStyle.TypesWhenNeeded
                .Serialize(configFile, m_configurationFrame)
            End With

            configFile.Close()
        Catch ex As Exception
            UpdateStatus(String.Format("Failed to serialize {0} [{1}] configuration frame: {3}", Name, m_configurationFrame.IDCode, ex.Message))
            m_exceptionLogger.Log(ex)
        End Try

        ' Define measurement to signal cross reference dictionary
        m_signalReferences = New Dictionary(Of MeasurementKey, SignalReference)

        ' Initialize measurement list for each pmu keyed on the signal reference field
        With RetrieveData("SELECT * FROM ActiveDeviceMeasurements", connection).Rows
            For x As Integer = 0 To .Count - 1
                With .Item(x)
                    signal = New SignalReference(.Item("SignalReference").ToString())

                    ' Lookup cell index by acronym. Doing this work upfront will save a huge amount
                    ' of work during primary measurement sorting
                    If Not idLabelCellIndex.TryGetValue(signal.Acronym, signal.CellIndex) Then
                        ' We cache these indicies locally to speed up initialization - we'll be
                        ' requesting these indexes for the same PMU's over and over
                        signal.CellIndex = m_configurationFrame.Cells.IndexOfIDLabel(signal.Acronym)
                        idLabelCellIndex.Add(signal.Acronym, signal.CellIndex)
                    End If

                    m_signalReferences.Add(New MeasurementKey(Convert.ToInt32(.Item("PointID")), .Item("Historian").ToString()), signal)
                End With
            Next
        End With

    End Sub

    Public ReadOnly Property ConfigurationFrame() As IConfigurationFrame
        Get
            Return m_configurationFrame
        End Get
    End Property

    Public Overridable ReadOnly Property Name() As String Implements TVA.Services.IServiceComponent.Name
        Get
            Return m_name
        End Get
    End Property

    Public Overrides ReadOnly Property Status() As String Implements TVA.Services.IServiceComponent.Status
        Get
            With New StringBuilder
                .Append("Operational Status for ")
                .Append(Name)
                .Append(":"c)
                .Append(Environment.NewLine)
                If m_communicationServer IsNot Nothing Then
                    .Append(m_communicationServer.Status)
                End If
                .Append(MyBase.Status)
                Return .ToString()
            End With
        End Get
    End Property

    Public Overridable Property PublishDescriptor() As Boolean
        Get
            Return m_publishDescriptor
        End Get
        Set(ByVal value As Boolean)
            m_publishDescriptor = value
        End Set
    End Property

    Protected ReadOnly Property CommunicationServer() As ICommunicationServer
        Get
            Return m_communicationServer
        End Get
    End Property

    Protected ReadOnly Property ExceptionLogger() As GlobalExceptionLogger
        Get
            Return m_exceptionLogger
        End Get
    End Property

    Protected Overridable Sub UpdateStatus(ByVal status As String)

        RaiseEvent StatusMessage(String.Format("[{0}]: {1}", Name, status))

    End Sub

    Protected MustOverride Function CreateNewConfigurationFrame(ByVal baseConfiguration As IConfigurationFrame) As IConfigurationFrame

    Protected Overrides Sub AssignMeasurementToFrame(ByVal frame As IFrame, ByVal measurement As IMeasurement)

        ' Assign all time-aligned measurements to their appropriate PMU (i.e., data frame cell)
        Dim signalRef As SignalReference

        ' Look up signal reference from measurement key
        If m_signalReferences.TryGetValue(measurement.Key, signalRef) AndAlso signalRef.CellIndex > -1 Then
            ' Get associated data cell
            Dim dataCell As IDataCell = DirectCast(frame, IDataFrame).Cells(signalRef.CellIndex)
            Dim signalIndex As Integer = signalRef.Index

            ' Assign value to appropriate cell property based on signal type
            Select Case signalRef.Type
                Case SignalType.Angle
                    ' Assign "phase angle" measurement to data cell
                    Dim phasorValues As PhasorValueCollection = dataCell.PhasorValues
                    If phasorValues.Count >= signalIndex Then phasorValues(signalIndex - 1).Angle = Convert.ToSingle(measurement.AdjustedValue)
                Case SignalType.Magnitude
                    ' Assign "phase magnitude" measurement to data cell
                    Dim phasorValues As PhasorValueCollection = dataCell.PhasorValues
                    If phasorValues.Count >= signalIndex Then phasorValues(signalIndex - 1).Magnitude = Convert.ToSingle(measurement.AdjustedValue)
                Case SignalType.Frequency
                    ' Assign "frequency" measurement to data cell
                    dataCell.FrequencyValue.Frequency = Convert.ToSingle(measurement.AdjustedValue)
                Case SignalType.dfdt
                    ' Assign "df/dt" measurement to data cell
                    dataCell.FrequencyValue.DfDt = Convert.ToSingle(measurement.AdjustedValue)
                Case SignalType.Status
                    ' Assign "common status flags" measurement to data cell
                    dataCell.CommonStatusFlags = Convert.ToInt32(measurement.AdjustedValue)
                Case SignalType.Digital
                    ' Assign "digital" measurement to data cell
                    Dim digitalValues As DigitalValueCollection = dataCell.DigitalValues
                    If digitalValues.Count >= signalIndex Then digitalValues(signalIndex - 1).Value = Convert.ToInt16(measurement.AdjustedValue)
                Case SignalType.Analog
                    ' Assign "analog" measurement to data cell
                    Dim analogValues As AnalogValueCollection = dataCell.AnalogValues
                    If analogValues.Count >= signalIndex Then analogValues(signalIndex - 1).Value = Convert.ToSingle(measurement.AdjustedValue)
            End Select

            ' Track total measurements sorted for frame - this will become total measurements published
            frame.PublishedMeasurements += 1
        End If

    End Sub

    Protected Overrides Sub PublishFrame(ByVal frame As IFrame, ByVal index As Integer)

        Dim dataFrame As IDataFrame = DirectCast(frame, IDataFrame)

        ' Send a descriptor packet at the top of each minute...
        If m_publishDescriptor AndAlso index = 0 AndAlso dataFrame.Timestamp.Second = 0 Then
            ' Publish binary image over specified communication layer
            m_configurationFrame.Ticks = dataFrame.Ticks
            m_communicationServer.Multicast(m_configurationFrame.BinaryImage())
        End If

        ' Prior to publication, set data validity bits based on reception of all data values
        For Each dataCell As IDataCell In dataFrame.Cells
            If dataCell.DataIsValid Then dataCell.DataIsValid = dataCell.AllValuesAssigned
        Next

        ' Publish binary image over specified communication layer
        m_communicationServer.Multicast(dataFrame.BinaryImage())

    End Sub

    Protected Overridable Sub HandleIncomingData(ByVal commandBuffer As Byte())

        ' This is optionally overridden to handle incoming data - such as IEEE commands

    End Sub

    Private Sub PhasorDataConcentrator_ProcessException(ByVal ex As System.Exception) Handles Me.ProcessException

        UpdateStatus(String.Format("Processing exception: {0}", ex.Message))
        m_exceptionLogger.Log(ex)

    End Sub

    Private Sub PhasorDataConcentrator_UnpublishedSamples(ByVal total As Integer) Handles Me.UnpublishedSamples

        If total > 2 * LagTime Then UpdateStatus(String.Format("There are {0} unpublished samples in the concentration queue...", total))

    End Sub

    Private Sub m_communicationServer_ClientConnected(ByVal sender As Object, ByVal e As TVA.GenericEventArgs(Of System.Guid)) Handles m_communicationServer.ClientConnected

        UpdateStatus("Client connected.")

    End Sub

    Private Sub m_communicationServer_ClientDisconnected(ByVal sender As Object, ByVal e As TVA.GenericEventArgs(Of System.Guid)) Handles m_communicationServer.ClientDisconnected

        UpdateStatus("Client disconnected.")

    End Sub

    Private Sub m_communicationServer_ServerStarted(ByVal sender As Object, ByVal e As System.EventArgs) Handles m_communicationServer.ServerStarted

        UpdateStatus("Server started.")

    End Sub

    Private Sub m_communicationServer_ServerStopped(ByVal sender As Object, ByVal e As System.EventArgs) Handles m_communicationServer.ServerStopped

        UpdateStatus("Server stopped.")

    End Sub

    Private Sub m_communicationServer_ServerStartupException(ByVal sender As Object, ByVal e As TVA.GenericEventArgs(Of System.Exception)) Handles m_communicationServer.ServerStartupException

        UpdateStatus(String.Format("Server startup exception: {0}", e.Argument.Message))

    End Sub

    Private Sub m_communicationServer_ReceivedClientData(ByVal sender As Object, ByVal e As TVA.GenericEventArgs(Of TVA.IdentifiableItem(Of System.Guid, Byte()))) Handles m_communicationServer.ReceivedClientData

        HandleIncomingData(e.Argument.Item)

    End Sub

    Protected Overridable Sub ProcessStateChanged(ByVal processName As String, ByVal newState As TVA.Services.ProcessState) Implements TVA.Services.IServiceComponent.ProcessStateChanged

        ' We don't normally handle changes in process state - but dervived classes may choose to...

    End Sub

    Protected Overridable Sub ServiceStateChanged(ByVal newState As TVA.Services.ServiceState) Implements TVA.Services.IServiceComponent.ServiceStateChanged

        Select Case newState
            Case ServiceState.Paused
                ' Pause concentrator
                Me.Enabled = False
                UpdateStatus("Data concentration paused at the request of service manager...")
            Case ServiceState.Resumed
                ' Resume concentrator
                Me.Enabled = True
                UpdateStatus("Data concentration resumed...")
        End Select

    End Sub

End Class
