﻿//******************************************************************************************************
//  CommonPhasorServices.cs - Gbtc
//
//  Copyright © 2012, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  4/9/2010 - J. Ritchie Carroll
//       Generated original version of source code.
//  3/11/2011 - Mehulbhai P Thakkar
//       Fixed bug in PhasorDataSourceValidation when CompanyID is NULL in Device table.
//  12/04/2012 - J. Ritchie Carroll
//       Migrated to PhasorProtocolAdapters project.
//  12/13/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using GSF;
using GSF.Configuration;
using GSF.Data;
using GSF.IO;
using GSF.PhasorProtocols;
using GSF.PhasorProtocols.Anonymous;
using GSF.TimeSeries;
using GSF.TimeSeries.Adapters;
using GSF.TimeSeries.Statistics;
using GSF.Units;

namespace PhasorProtocolAdapters
{
    /// <summary>
    /// Provides common phasor services.
    /// </summary>
    /// <remarks>
    /// Typically class should be implemented as a singleton since one instance will suffice.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class CommonPhasorServices : FacileActionAdapterBase
    {
        #region [ Members ]

        // Fields
        private IAdapterCollection m_parent;
        private InputAdapterCollection m_inputAdapters;
        private ActionAdapterCollection m_actionAdapters;
        //private OutputAdapterCollection m_outputAdapters;
        private ManualResetEvent m_configurationWaitHandle;
        private MultiProtocolFrameParser m_frameParser;
        private IConfigurationFrame m_configurationFrame;
        private bool m_disposed;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new <see cref="CommonPhasorServices"/>.
        /// </summary>
        public CommonPhasorServices()
        {
            // Create wait handle to use to wait for configuration frame
            m_configurationWaitHandle = new ManualResetEvent(false);

            // Create a new phasor protocol frame parser used to dynamically request device configuration frames
            // and return them to remote clients so that the frame can be used in system setup and configuration
            m_frameParser = new MultiProtocolFrameParser();

            // Attach to events on new frame parser reference
            m_frameParser.ConnectionAttempt += m_frameParser_ConnectionAttempt;
            m_frameParser.ConnectionEstablished += m_frameParser_ConnectionEstablished;
            m_frameParser.ConnectionException += m_frameParser_ConnectionException;
            m_frameParser.ConnectionTerminated += m_frameParser_ConnectionTerminated;
            m_frameParser.ExceededParsingExceptionThreshold += m_frameParser_ExceededParsingExceptionThreshold;
            m_frameParser.ParsingException += m_frameParser_ParsingException;
            m_frameParser.ReceivedConfigurationFrame += m_frameParser_ReceivedConfigurationFrame;

            // We only want to try to connect to device and retrieve configuration as quickly as possible
            m_frameParser.MaximumConnectionAttempts = 1;
            m_frameParser.SourceName = Name;
            m_frameParser.AutoRepeatCapturedPlayback = false;
            m_frameParser.AutoStartDataParsingSequence = false;
            m_frameParser.SkipDisableRealTimeData = true;
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets the flag indicating if this adapter supports temporal processing.
        /// </summary>
        /// <remarks>
        /// Since the common phasor services is designed to assist in various real-time operations,
        /// it is expected that this would not be desired in a temporal data streaming session.
        /// </remarks>
        public override bool SupportsTemporalProcessing
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="CommonPhasorServices"/> object and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                try
                {
                    if (disposing)
                    {
                        // Detach from frame parser events and dispose
                        if ((object)m_frameParser != null)
                        {
                            m_frameParser.ConnectionAttempt -= m_frameParser_ConnectionAttempt;
                            m_frameParser.ConnectionEstablished -= m_frameParser_ConnectionEstablished;
                            m_frameParser.ConnectionException -= m_frameParser_ConnectionException;
                            m_frameParser.ConnectionTerminated -= m_frameParser_ConnectionTerminated;
                            m_frameParser.ExceededParsingExceptionThreshold -= m_frameParser_ExceededParsingExceptionThreshold;
                            m_frameParser.ParsingException -= m_frameParser_ParsingException;
                            m_frameParser.ReceivedConfigurationFrame -= m_frameParser_ReceivedConfigurationFrame;
                            m_frameParser.Dispose();
                        }
                        m_frameParser = null;

                        // Dispose configuration of wait handle
                        if (m_configurationWaitHandle != null)
                            m_configurationWaitHandle.Close();

                        m_configurationWaitHandle = null;
                        m_configurationFrame = null;
                    }
                }
                finally
                {
                    m_disposed = true;          // Prevent duplicate dispose.
                    base.Dispose(disposing);    // Call base class Dispose().
                }
            }
        }

        /// <summary>
        /// Assigns the reference to the parent <see cref="IAdapterCollection"/> that will contain this <see cref="AdapterBase"/>.
        /// </summary>
        /// <param name="parent">Parent adapter collection.</param>
        protected override void AssignParentCollection(IAdapterCollection parent)
        {
            base.AssignParentCollection(parent);

            m_parent = parent;

            if (parent != null)
            {
                // Dereference primary Iaon adapter collections
                m_inputAdapters = m_parent.Parent.Where(collection => collection is InputAdapterCollection).First() as InputAdapterCollection;
                m_actionAdapters = m_parent.Parent.Where(collection => collection is ActionAdapterCollection).First() as ActionAdapterCollection;
                //m_outputAdapters = m_parent.Parent.Where(collection => collection is OutputAdapterCollection).First() as OutputAdapterCollection;
            }
            else
            {
                m_inputAdapters = null;
                m_actionAdapters = null;
                //m_outputAdapters = null;
            }
        }

        /// <summary>
        /// Gets a short one-line status of this <see cref="CommonPhasorServices"/>.
        /// </summary>
        /// <param name="maxLength">Maximum number of available characters for display.</param>
        /// <returns>A short one-line summary of the current status of the <see cref="CommonPhasorServices"/>.</returns>
        public override string GetShortStatus(int maxLength)
        {
            return "Type \"LISTCOMMANDS 0\" to enumerate service commands.".CenterText(maxLength);
        }

        /// <summary>
        /// Requests a configuration frame from a phasor device.
        /// </summary>
        /// <param name="connectionString">Connection string used to connect to phasor device.</param>
        /// <returns>A <see cref="IConfigurationFrame"/> if successful, -or- <c>null</c> if request failed.</returns>
        [AdapterCommand("Connects to a phasor device and requests its configuration frame.")]
        public IConfigurationFrame RequestDeviceConfiguration(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                OnStatusMessage("ERROR: No connection string was specified, request for configuration canceled.");
                return new ConfigurationErrorFrame();
            }

            // Define a line of asterisks for emphasis
            string stars = new string('*', 79);

            // Only allow configuration request if another request is not already pending...
            if (Monitor.TryEnter(m_frameParser))
            {
                try
                {
                    Dictionary<string, string> settings = connectionString.ParseKeyValuePairs();
                    string setting;
                    ushort accessID;

                    // Get accessID from connection string
                    if (settings.TryGetValue("accessID", out setting))
                        accessID = ushort.Parse(setting);
                    else
                        accessID = 1;

                    // Most of the parameters in the connection string will be for the data source in the frame parser
                    // so we provide all of them, other parameters will simply be ignored
                    m_frameParser.ConnectionString = connectionString;

                    // Provide access ID to frame parser as this may be necessary to make a phasor connection
                    m_frameParser.DeviceID = accessID;

                    // Clear any existing configuration frame
                    m_configurationFrame = null;

                    // Inform user of temporary loss of command access
                    OnStatusMessage("\r\n{0}\r\n\r\nAttempting to request remote device configuration.\r\n\r\nThis request could take up to sixty seconds to complete.\r\n\r\nNo other commands will be accepted until this request succeeds or fails.\r\n\r\n{0}", stars, stars);

                    // Make sure the wait handle is not set
                    m_configurationWaitHandle.Reset();

                    // Start the frame parser - this will attempt connection
                    m_frameParser.Start();

                    // We wait a maximum of 60 seconds to receive the configuration frame - this delay should be the maximum time ever needed
                    // to receive a configuration frame. If the device connection is Active or Hybrid then the configuration frame should be
                    // returned immediately - for purely Passive connections the configuration frame is delivered once per minute.
                    if (!m_configurationWaitHandle.WaitOne(60000))
                        OnStatusMessage("WARNING: Timed-out waiting to retrieve remote device configuration.");

                    // Terminate connection to device
                    m_frameParser.Stop();

                    if (m_configurationFrame == null)
                    {
                        m_configurationFrame = new ConfigurationErrorFrame();
                        OnStatusMessage("Failed to retrieve remote device configuration.");
                    }

                    return m_configurationFrame;
                }
                catch (Exception ex)
                {
                    OnStatusMessage("ERROR: Failed to request configuration due to exception: {0}", ex.Message);
                }
                finally
                {
                    // Release the lock
                    Monitor.Exit(m_frameParser);

                    // Inform user of restoration of command access
                    OnStatusMessage("\r\n{0}\r\n\r\nRemote device configuration request completed.\r\n\r\nCommand access has been restored.\r\n\r\n{0}", stars, stars);
                }
            }
            else
            {
                OnStatusMessage("ERROR: Cannot process simultaneous requests for device configurations, please try again in a few seconds..");
            }

            return new ConfigurationErrorFrame();
        }

        /// <summary>
        /// Sends the specified <see cref="DeviceCommand"/> to the current device connection.
        /// </summary>
        /// <param name="command"><see cref="DeviceCommand"/> to send to connected device.</param>
        public void SendCommand(DeviceCommand command)
        {
            if ((object)m_frameParser != null)
            {
                m_frameParser.SendDeviceCommand(command);
                OnStatusMessage("Sent device command \"{0}\"...", command);
            }
            else
            {
                OnStatusMessage("Failed to send device command \"{0}\", no frame parser is defined.", command);
            }
        }

        private void m_frameParser_ReceivedConfigurationFrame(object sender, EventArgs<IConfigurationFrame> e)
        {
            // Cache received configuration frame
            m_configurationFrame = e.Argument;

            OnStatusMessage("Successfully received configuration frame!");

            // Clear wait handle
            m_configurationWaitHandle.Set();
        }

        private void m_frameParser_ConnectionTerminated(object sender, EventArgs e)
        {
            // Communications layer closed connection (close not initiated by system) - so we cancel request..
            if (m_frameParser.Enabled)
                OnStatusMessage("ERROR: Connection closed by remote device, request for configuration canceled.");

            // Clear wait handle
            m_configurationWaitHandle.Set();
        }

        private void m_frameParser_ConnectionEstablished(object sender, EventArgs e)
        {
            OnStatusMessage("Connected to remote device, requesting configuration frame...");

            // Send manual request for configuration frame
            SendCommand(DeviceCommand.SendConfigurationFrame2);
        }

        private void m_frameParser_ConnectionException(object sender, EventArgs<Exception, int> e)
        {
            OnStatusMessage("ERROR: Connection attempt failed, request for configuration canceled: {0}", e.Argument1.Message);

            // Clear wait handle
            m_configurationWaitHandle.Set();
        }

        private void m_frameParser_ParsingException(object sender, EventArgs<Exception> e)
        {
            OnStatusMessage("ERROR: Parsing exception during request for configuration: {0}", e.Argument.Message);
        }

        private void m_frameParser_ExceededParsingExceptionThreshold(object sender, EventArgs e)
        {
            OnStatusMessage("\r\nRequest for configuration canceled due to an excessive number of exceptions...\r\n");

            // Clear wait handle
            m_configurationWaitHandle.Set();
        }

        private void m_frameParser_ConnectionAttempt(object sender, EventArgs e)
        {
            OnStatusMessage("Attempting {0} {1} based connection...", m_frameParser.PhasorProtocol.GetFormattedProtocolName(), m_frameParser.TransportProtocol.ToString().ToUpper());
        }

        #endregion

        #region [ Static ]

        // Static Fields
        private static readonly StatisticValueStateCache s_statisticValueCache = new StatisticValueStateCache();

        // Static Methods

        /// <summary>
        /// Apply start-up phasor data source validations
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="adapterType">The database adapter type.</param>
        /// <param name="nodeIDQueryString">Current node ID in proper query format.</param>
        /// <param name="arguments">Arguments, if any, to be used but the data source validation.</param>
        /// <param name="statusMessage">The delegate which will display a status message to the user.</param>
        /// <param name="processException">The delegate which will handle exception logging.</param>
        [SuppressMessage("Microsoft.Maintainability", "CA1502"), SuppressMessage("Microsoft.Maintainability", "CA1505")]
        private static void PhasorDataSourceValidation(IDbConnection connection, Type adapterType, string nodeIDQueryString, string arguments, Action<object, EventArgs<string>> statusMessage, Action<object, EventArgs<Exception>> processException)
        {
            // Make sure setting exists to allow user to by-pass phasor data source validation at startup
            ConfigurationFile configFile = ConfigurationFile.Current;
            CategorizedSettingsElementCollection settings = configFile.Settings["systemSettings"];
            settings.Add("ProcessPhasorDataSourceValidation", true, "Determines if the phasor data source validation should be processed at startup");

            // See if this node should process phasor source validation
            if (settings["ProcessPhasorDataSourceValidation"].ValueAsBoolean())
            {
                Dictionary<string, string> args = new Dictionary<string, string>();
                bool skipOptimization = false;
                string arg;

                if (!string.IsNullOrEmpty(arguments))
                    args = arguments.ParseKeyValuePairs();

                if (args.TryGetValue("skipOptimization", out arg))
                    skipOptimization = arg.ParseBoolean();

                CreateDefaultNode(connection, nodeIDQueryString, statusMessage, processException);
                LoadDefaultConfigurationEntity(connection, statusMessage, processException);
                LoadDefaultInterconnection(connection, statusMessage, processException);
                LoadDefaultProtocol(connection, statusMessage, processException);
                LoadDefaultSignalType(connection, statusMessage, processException);
                LoadDefaultStatistic(connection, statusMessage, processException);
                EstablishDefaultMeasurementKeyCache(connection, adapterType, statusMessage, processException);

                statusMessage("CommonPhasorServices", new EventArgs<string>("Validating signal types..."));

                // Validate that the acronym for status flags is FLAG (it was STAT in prior versions)
                if (connection.ExecuteScalar("SELECT Acronym FROM SignalType WHERE Suffix='SF'").ToNonNullString().ToUpper() == "STAT")
                    connection.ExecuteNonQuery("UPDATE SignalType SET Acronym='FLAG' WHERE Suffix='SF'");

                // Validate that the calculation and statistic signal types are defined (they did not in initial release)
                if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM SignalType WHERE Acronym='CALC'")) == 0)
                    connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Calculated Value', 'CALC', 'CV', 'C', 'PMU', '')");

                if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM SignalType WHERE Acronym='STAT'")) == 0)
                    connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Statistic', 'STAT', 'ST', 'P', 'Any', '')");

                statusMessage("CommonPhasorServices", new EventArgs<string>("Validating output stream device ID codes..."));

                // Validate all ID codes for output stream devices are not set their default value
                connection.ExecuteNonQuery("UPDATE OutputStreamDevice SET IDCode=ID WHERE IDCode=0");

                statusMessage("CommonPhasorServices", new EventArgs<string>("Verifying statistics archive exists..."));

                // Validate that the statistics historian exists
                if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Historian WHERE Acronym='STAT' AND NodeID={0}", nodeIDQueryString))) == 0)
                    connection.ExecuteNonQuery(string.Format("INSERT INTO Historian(NodeID, Acronym, Name, AssemblyName, TypeName, ConnectionString, IsLocal, Description, LoadOrder, Enabled) VALUES({0}, 'STAT', 'Statistics Archive', 'HistorianAdapters.dll', 'HistorianAdapters.LocalOutputAdapter', '', 1, 'Local historian used to archive system statistics', 9999, 1)", nodeIDQueryString));

                // Make sure statistics path exists to hold historian files
                string statisticsPath = FilePath.GetAbsolutePath(FilePath.AddPathSuffix("Statistics"));

                if (!Directory.Exists(statisticsPath))
                    Directory.CreateDirectory(statisticsPath);

                // Make sure needed statistic historian configuration settings are properly defined
                settings = configFile.Settings["statMetadataFile"];
                settings.Add("FileName", "Statistics\\stat_dbase.dat", "Name of the statistics meta-data file including its path.");
                settings.Add("LoadOnOpen", true, "True if file records are to be loaded in memory when opened; otherwise False - this defaults to True for the statistics meta-data file.");
                settings.Add("ReloadOnModify", false, "True if file records loaded in memory are to be re-loaded when file is modified on disk; otherwise False - this defaults to False for the statistics meta-data file.");
                settings["LoadOnOpen"].Update(true);
                settings["ReloadOnModify"].Update(false);

                settings = configFile.Settings["statStateFile"];
                settings.Add("FileName", "Statistics\\stat_startup.dat", "Name of the statistics state file including its path.");
                settings.Add("AutoSaveInterval", 10000, "Interval in milliseconds at which the file records loaded in memory are to be saved automatically to disk. Use -1 to disable automatic saving - this defaults to 10,000 for the statistics state file.");
                settings.Add("LoadOnOpen", true, "True if file records are to be loaded in memory when opened; otherwise False - this defaults to True for the statistics state file.");
                settings.Add("SaveOnClose", true, "True if file records loaded in memory are to be saved to disk when file is closed; otherwise False - this defaults to True for the statistics state file.");
                settings.Add("ReloadOnModify", false, "True if file records loaded in memory are to be re-loaded when file is modified on disk; otherwise False - this defaults to False for the statistics state file.");
                settings["AutoSaveInterval"].Update(10000);
                settings["LoadOnOpen"].Update(true);
                settings["SaveOnClose"].Update(true);
                settings["ReloadOnModify"].Update(false);

                settings = configFile.Settings["statIntercomFile"];
                settings.Add("FileName", "Statistics\\scratch.dat", "Name of the statistics intercom file including its path.");
                settings.Add("AutoSaveInterval", 10000, "Interval in milliseconds at which the file records loaded in memory are to be saved automatically to disk. Use -1 to disable automatic saving - this defaults to 10,000 for the statistics intercom file.");
                settings.Add("LoadOnOpen", true, "True if file records are to be loaded in memory when opened; otherwise False - this defaults to True for the statistics intercom file.");
                settings.Add("SaveOnClose", true, "True if file records loaded in memory are to be saved to disk when file is closed; otherwise False - this defaults to True for the statistics intercom file.");
                settings.Add("ReloadOnModify", false, "True if file records loaded in memory are to be re-loaded when file is modified on disk; otherwise False - this defaults to False for the statistics intercom file.");
                settings["AutoSaveInterval"].Update(1000);
                settings["LoadOnOpen"].Update(true);
                settings["SaveOnClose"].Update(true);
                settings["ReloadOnModify"].Update(false);

                settings = configFile.Settings["statArchiveFile"];
                settings.Add("FileName", "Statistics\\stat_archive.d", "Name of the statistics working archive file including its path.");
                settings.Add("CacheWrites", true, "True if writes are to be cached for performance; otherwise False - this defaults to True for the statistics working archive file.");
                settings.Add("ConserveMemory", false, "True if attempts are to be made to conserve memory; otherwise False - this defaults to False for the statistics working archive file.");
                settings["CacheWrites"].Update(true);
                settings["ConserveMemory"].Update(false);

                settings = configFile.Settings["statMetadataService"];
                settings.Add("Endpoints", "http.rest://localhost:6051/historian", "Semicolon delimited list of URIs where the web service can be accessed - this defaults to http.rest://localhost:6051/historian for the statistics meta-data service.");

                settings = configFile.Settings["statTimeSeriesDataService"];
                settings.Add("Endpoints", "http.rest://localhost:6052/historian", "Semicolon delimited list of URIs where the web service can be accessed - this defaults to http.rest://localhost:6052/historian for the statistics time-series data service.");

                configFile.Save();

                // Get the needed statistic related IDs
                int statSignalTypeID = Convert.ToInt32(connection.ExecuteScalar("SELECT ID FROM SignalType WHERE Acronym='STAT'"));
                int statHistorianID = Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT ID FROM Historian WHERE Acronym='STAT' AND NodeID={0}", nodeIDQueryString)));
                object nodeCompanyID = connection.ExecuteScalar(string.Format("SELECT CompanyID FROM Node WHERE ID={0}", nodeIDQueryString));

                // Load the defined system statistics
                IEnumerable<DataRow> statistics = connection.RetrieveData(adapterType, "SELECT * FROM Statistic ORDER BY Source, SignalIndex").AsEnumerable();

                // Filter statistics to device, input stream and output stream types            
                IEnumerable<DataRow> deviceStatistics = statistics.Where(row => string.Compare(row.Field<string>("Source"), "Device", true) == 0).ToList();
                IEnumerable<DataRow> inputStreamStatistics = statistics.Where(row => string.Compare(row.Field<string>("Source"), "InputStream", true) == 0).ToList();
                IEnumerable<DataRow> outputStreamStatistics = statistics.Where(row => string.Compare(row.Field<string>("Source"), "OutputStream", true) == 0).ToList();

                IEnumerable<DataRow> outputStreamDevices;
                SignalKind[] validOutputSignalKinds = { SignalKind.Angle, SignalKind.Magnitude, SignalKind.Frequency, SignalKind.DfDt, SignalKind.Status, SignalKind.Analog, SignalKind.Digital, SignalKind.Calculation };
                List<int> measurementIDsToDelete = new List<int>();
                SignalReference deviceSignalReference;
                string query, acronym, signalReference, pointTag, company, vendorDevice, description, protocolIDs;
                int adapterID, signalIndex;
                bool firstStatisticExisted;

                statusMessage("CommonPhasorServices", new EventArgs<string>("Validating device protocols..."));

                // Extract IDs for phasor protocols
                StringBuilder protocolIDList = new StringBuilder();
                DataTable protocols = connection.RetrieveData(adapterType, "SELECT * FROM Protocol");

                if (protocols.Columns.Contains("Category"))
                {
                    // Make sure new protocol types exist
                    if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Protocol WHERE Acronym='GatewayTransport'"))) == 0)
                    {
                        connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('GatewayTransport', 'Gateway Transport', 'Measurement', 'Gateway', 'GSF.TimeSeries.dll', 'GSF.TimeSeries.Transport.DataSubscriber', " + (protocols.Rows.Count + 1) + ")");

                        if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Protocol WHERE Acronym='WAV'"))) == 0)
                            connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('WAV', 'Wave Form Input Adapter', 'Frame', 'Audio', 'WavInputAdapter.dll', 'WavInputAdapter.WavInputAdapter', " + (protocols.Rows.Count + 2) + ")");

                        if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Protocol WHERE Acronym='IeeeC37_118V2'"))) == 0)
                            connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('IeeeC37_118V2', 'IEEE C37.118.2-2011', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 2)");

                        if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Protocol WHERE Acronym='VirtualInput'"))) == 0)
                            connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('VirtualInput', 'Virtual Device', 'Frame', 'Virtual', 'TestingAdapters.dll', 'TestingAdapters.VirtualInputAdapter', " + (protocols.Rows.Count + 4) + ")");
                    }

                    if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Protocol WHERE Acronym='Iec61850_90_5'"))) == 0)
                        connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('Iec61850_90_5', 'IEC 61850-90-5', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 12)");

                    foreach (DataRow protocol in protocols.Rows)
                    {
                        if (string.Compare(protocol.Field<string>("Category"), "Phasor", true) == 0)
                        {
                            if (protocolIDList.Length > 0)
                                protocolIDList.Append(", ");

                            protocolIDList.Append(protocol.ConvertField<int>("ID"));
                        }
                    }
                }
                else
                {
                    // Older schemas do not include protocol categories and assembly info
                    foreach (DataRow protocol in protocols.Rows)
                    {
                        if (protocolIDList.Length > 0)
                            protocolIDList.Append(", ");

                        protocolIDList.Append(protocol.ConvertField<int>("ID"));
                    }
                }

                protocolIDs = protocolIDList.ToString();

                statusMessage("CommonPhasorServices", new EventArgs<string>("Validating device measurements..."));

                // Make sure needed device statistic measurements exist, currently statistics are only associated with phasor devices so we filter based on protocol
                foreach (DataRow device in connection.RetrieveData(adapterType, string.Format("SELECT * FROM Device WHERE IsConcentrator = 0 AND NodeID = {0} AND ProtocolID IN ({1})", nodeIDQueryString, protocolIDs)).Rows)
                {
                    firstStatisticExisted = true;

                    foreach (DataRow statistic in deviceStatistics)
                    {
                        string oldAcronym;
                        string oldSignalReference;

                        signalIndex = statistic.ConvertField<int>("SignalIndex");
                        oldAcronym = device.Field<string>("Acronym");
                        acronym = oldAcronym + "!PMU";
                        oldSignalReference = SignalReference.ToString(oldAcronym, SignalKind.Statistic, signalIndex);
                        signalReference = SignalReference.ToString(acronym, SignalKind.Statistic, signalIndex);

                        // If the original format for device statistics is found in the database, update to new format
                        if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Measurement WHERE SignalReference='{0}' AND HistorianID={1}", oldSignalReference, statHistorianID))) > 0)
                        {
                            connection.ExecuteNonQuery(string.Format("UPDATE Measurement SET SignalReference='{0}' WHERE SignalReference='{1}' AND HistorianID={2}", signalReference, oldSignalReference, statHistorianID));

                            // No need to insert it since we
                            // can guarantee its existence
                            continue;
                        }

                        if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Measurement WHERE SignalReference='{0}' AND HistorianID={1}", signalReference, statHistorianID))) == 0)
                        {
                            firstStatisticExisted = false;
                            company = (string)connection.ExecuteScalar(string.Format("SELECT MapAcronym FROM Company WHERE ID={0}", device.ConvertNullableField<int>("CompanyID") ?? 0));
                            if (string.IsNullOrEmpty(company))
                                company = configFile.Settings["systemSettings"]["CompanyAcronym"].Value.TruncateRight(3);

                            vendorDevice = (string)connection.ExecuteScalar(string.Format("SELECT Name FROM VendorDevice WHERE ID={0}", device.ConvertNullableField<int>("VendorDeviceID") ?? 0));
                            pointTag = string.Format("{0}_{1}:ST{2}", company, acronym, signalIndex);
                            description = string.Format("{0} {1} Statistic for {2}", device.Field<string>("Name"), vendorDevice, statistic.Field<string>("Description"));

                            query = ParameterizedQueryString(adapterType, "INSERT INTO Measurement(HistorianID, DeviceID, PointTag, SignalTypeID, PhasorSourceIndex, " +
                                "SignalReference, Description, Enabled) VALUES({0}, {1}, {2}, {3}, NULL, {4}, {5}, 1)", "statHistorianID", "deviceID", "pointTag",
                                "statSignalTypeID", "signalReference", "description");

                            using (IDbCommand command = connection.CreateParameterizedCommand(query, statHistorianID, device.ConvertField<int>("ID"), pointTag, statSignalTypeID, signalReference, description))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // To reduce time required to execute these steps, only first statistic is verfied to exist
                            if (!skipOptimization && firstStatisticExisted)
                                break;
                        }
                    }
                }

                statusMessage("CommonPhasorServices", new EventArgs<string>("Validating input stream measurements..."));

                // Make sure needed input stream statistic measurements exist, currently statistics are only associated with phasor devices so we filter based on protocol
                foreach (DataRow inputStream in connection.RetrieveData(adapterType, string.Format("SELECT * FROM Device WHERE ((IsConcentrator <> 0) OR (IsConcentrator = 0 AND ParentID IS NULL)) AND NodeID = {0} AND ProtocolID IN ({1})", nodeIDQueryString, protocolIDs)).Rows)
                {
                    firstStatisticExisted = true;

                    foreach (DataRow statistic in inputStreamStatistics)
                    {
                        acronym = inputStream.Field<string>("Acronym") + "!IS";
                        signalIndex = statistic.ConvertField<int>("SignalIndex");
                        signalReference = SignalReference.ToString(acronym, SignalKind.Statistic, signalIndex);

                        if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Measurement WHERE SignalReference='{0}' AND HistorianID={1}", signalReference, statHistorianID))) == 0)
                        {
                            firstStatisticExisted = false;
                            company = (string)connection.ExecuteScalar(string.Format("SELECT MapAcronym FROM Company WHERE ID={0}", inputStream.ConvertNullableField<int>("CompanyID") ?? 0));
                            if (string.IsNullOrEmpty(company))
                                company = configFile.Settings["systemSettings"]["CompanyAcronym"].Value.TruncateRight(3);

                            vendorDevice = (string)connection.ExecuteScalar(string.Format("SELECT Name FROM VendorDevice WHERE ID={0}", inputStream.ConvertNullableField<int>("VendorDeviceID") ?? 0)); // Modified to retrieve VendorDeviceID into Nullable of Int as it is not a required field.
                            pointTag = string.Format("{0}_{1}:ST{2}", company, acronym, signalIndex);
                            description = string.Format("{0} {1} Statistic for {2}", inputStream.Field<string>("Name"), vendorDevice, statistic.Field<string>("Description"));

                            query = ParameterizedQueryString(adapterType, "INSERT INTO Measurement(HistorianID, DeviceID, PointTag, SignalTypeID, PhasorSourceIndex, " +
                                "SignalReference, Description, Enabled) VALUES({0}, {1}, {2}, {3}, NULL, {4}, {5}, 1)", "statHistorianID", "deviceID", "pointTag",
                                "statSignalTypeID", "signalReference", "description");

                            using (IDbCommand command = connection.CreateParameterizedCommand(query, statHistorianID, inputStream.ConvertField<int>("ID"), pointTag, statSignalTypeID, signalReference, description))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // To reduce time required to execute these steps, only first statistic is verfied to exist
                            if (!skipOptimization && firstStatisticExisted)
                                break;
                        }
                    }
                }

                // Make sure devices associated with a concentrator do not have any extraneous input stream statistic measurements - this can happen
                // when a device was once a direct connect device but now is part of a concentrator...
                foreach (DataRow inputStream in connection.RetrieveData(adapterType, string.Format("SELECT * FROM Device WHERE (IsConcentrator = 0 AND ParentID IS NOT NULL) AND NodeID = {0} AND ProtocolID IN ({1})", nodeIDQueryString, protocolIDs)).Rows)
                {
                    firstStatisticExisted = false;

                    foreach (DataRow statistic in inputStreamStatistics)
                    {
                        acronym = inputStream.Field<string>("Acronym") + "!IS";
                        signalIndex = statistic.ConvertField<int>("SignalIndex");
                        signalReference = SignalReference.ToString(acronym, SignalKind.Statistic, signalIndex);

                        // To reduce time required to execute these steps, only first statistic is verfied to exist
                        if (!skipOptimization && !firstStatisticExisted)
                        {
                            firstStatisticExisted = (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Measurement WHERE SignalReference='{0}'", signalReference))) > 0);

                            // If the first extraneous input statistic doesn't exist, we assume no others do as well
                            if (!firstStatisticExisted)
                                break;
                        }

                        // Remove extraneous input statistics
                        connection.ExecuteNonQuery(string.Format("DELETE FROM Measurement WHERE SignalReference = '{0}'", signalReference));
                    }
                }

                statusMessage("CommonPhasorServices", new EventArgs<string>("Validating output stream measurements..."));

                // Make sure needed output stream statistic measurements exist
                foreach (DataRow outputStream in connection.RetrieveData(adapterType, string.Format("SELECT * FROM OutputStream WHERE NodeID = {0}", nodeIDQueryString)).Rows)
                {
                    firstStatisticExisted = true;
                    adapterID = outputStream.ConvertField<int>("ID");
                    acronym = outputStream.Field<string>("Acronym") + "!OS";

                    foreach (DataRow statistic in outputStreamStatistics)
                    {
                        signalIndex = statistic.ConvertField<int>("SignalIndex");
                        signalReference = SignalReference.ToString(acronym, SignalKind.Statistic, signalIndex);

                        if (Convert.ToInt32(connection.ExecuteScalar(string.Format("SELECT COUNT(*) FROM Measurement WHERE SignalReference='{0}' AND HistorianID={1}", signalReference, statHistorianID))) == 0)
                        {
                            firstStatisticExisted = false;
                            if (nodeCompanyID is DBNull)
                                company = configFile.Settings["systemSettings"]["CompanyAcronym"].Value.TruncateRight(3);
                            else
                                company = (string)connection.ExecuteScalar(string.Format("SELECT MapAcronym FROM Company WHERE ID={0}", nodeCompanyID));

                            pointTag = string.Format("{0}_{1}:ST{2}", company, acronym, signalIndex);
                            description = string.Format("{0} Statistic for {1}", outputStream.Field<string>("Name"), statistic.Field<string>("Description"));

                            query = ParameterizedQueryString(adapterType, "INSERT INTO Measurement(HistorianID, DeviceID, PointTag, SignalTypeID, PhasorSourceIndex, " +
                                "SignalReference, Description, Enabled) VALUES({0}, NULL, {1}, {2}, NULL, {3}, {4}, 1)", "statHistorianID", "pointTag", "statSignalTypeID",
                                "signalReference", "description");

                            using (IDbCommand command = connection.CreateParameterizedCommand(query, statHistorianID, pointTag, statSignalTypeID, signalReference, description))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // To reduce time required to execute these steps, only first statistic is verfied to exist
                            if (!skipOptimization && firstStatisticExisted)
                                break;
                        }
                    }

                    // Load devices associated with this output stream
                    outputStreamDevices = connection.RetrieveData(adapterType, string.Format("SELECT * FROM OutputStreamDevice WHERE AdapterID = {0} AND NodeID = {1}", adapterID, nodeIDQueryString)).AsEnumerable();

                    // Validate measurements associated with this output stream
                    foreach (DataRow outputStreamMeasurement in connection.RetrieveData(adapterType, string.Format("SELECT * FROM OutputStreamMeasurement WHERE AdapterID = {0} AND NodeID = {1}", adapterID, nodeIDQueryString)).Rows)
                    {
                        // Parse output stream measurement signal reference
                        deviceSignalReference = new SignalReference(outputStreamMeasurement.Field<string>("SignalReference"));

                        // Validate that the signal reference is associated with one of the output stream's devices
                        if (!outputStreamDevices.Any(row => string.Compare(row.Field<string>("Acronym"), deviceSignalReference.Acronym, true) == 0))
                        {
                            // This measurement has a signal reference for a device that is not part of the associated output stream, so we mark it for deletion
                            measurementIDsToDelete.Add(outputStreamMeasurement.ConvertField<int>("ID"));
                        }
                        else
                        {
                            // Validate that the signal reference type is valid for an output stream
                            if (!validOutputSignalKinds.Any(type => type == deviceSignalReference.Kind))
                            {
                                // This measurement has a signal reference type that is invalid for an output stream, so we mark it for deletion
                                measurementIDsToDelete.Add(outputStreamMeasurement.ConvertField<int>("ID"));
                            }
                        }
                    }
                }

                if (measurementIDsToDelete.Count > 0)
                {
                    statusMessage("CommonPhasorServices", new EventArgs<string>(string.Format("Removing {0} unused output stream device measurements...", measurementIDsToDelete.Count)));

                    foreach (int measurementID in measurementIDsToDelete)
                    {
                        connection.ExecuteNonQuery(string.Format("DELETE FROM OutputStreamMeasurement WHERE ID = {0} AND NodeID = {1}", measurementID, nodeIDQueryString));
                    }
                }

                if (skipOptimization)
                {
                    // If skipOptimization is set to true, automatically set it back to false
                    string unskipOptimizationQuery = "UPDATE DataOperation SET Arguments = '' " +
                        "WHERE AssemblyName = 'PhasorProtocolAdapters.dll' " +
                        "AND TypeName = 'PhasorProtocolAdapters.CommonPhasorServices' " +
                        "AND MethodName = 'PhasorDataSourceValidation'";

                    connection.ExecuteNonQuery(unskipOptimizationQuery);
                }
            }
        }

        /// <summary>
        /// Creates a parameterized query string for the underlying database type 
        /// based on the given format string and the parameter names.
        /// </summary>
        /// <param name="adapterType">The adapter type used to determine the underlying database type.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="parameterNames">A string array that contains zero or more parameter names to format.</param>
        /// <returns>A parameterized query string based on the given format and parameter names.</returns>
        private static string ParameterizedQueryString(Type adapterType, string format, params string[] parameterNames)
        {
            bool oracle = adapterType.Name == "OracleDataAdapter";
            char paramChar = oracle ? ':' : '@';
            object[] parameters = parameterNames.Select(name => paramChar + name).ToArray();
            return string.Format(format, parameters);
        }

        /// <summary>
        /// Creates the default node for the Node table.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="nodeIDQueryString">The ID of the node in the proper database format.</param>
        /// <param name="statusMessage">The delegate which will display a status message to the user.</param>
        /// <param name="processException">The delegate which will handle exception logging.</param>
        private static void CreateDefaultNode(IDbConnection connection, string nodeIDQueryString, Action<object, EventArgs<string>> statusMessage, Action<object, EventArgs<Exception>> processException)
        {
            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM Node")) == 0)
            {
                statusMessage("CommonPhasorServices", new EventArgs<string>("Creating default record for Node..."));
                connection.ExecuteNonQuery("INSERT INTO Node(Name, CompanyID, Description, Settings, MenuType, MenuData, Master, LoadOrder, Enabled) VALUES('Default', NULL, 'Default node', 'RemoteStatusServerConnectionString={server=localhost:8500;integratedSecurity=true};datapublisherport=6165;AlarmServiceUrl=http://localhost:5018/alarmservices', 'File', 'Menu.xml', 1, 0, 1)");
                connection.ExecuteNonQuery("UPDATE Node SET ID=" + nodeIDQueryString);
            }
        }

        /// <summary>
        /// Loads the default configuration for the ConfigurationEntity table.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="statusMessage">The delegate which will display a status message to the user.</param>
        /// <param name="processException">The delegate which will handle exception logging.</param>
        private static void LoadDefaultConfigurationEntity(IDbConnection connection, Action<object, EventArgs<string>> statusMessage, Action<object, EventArgs<Exception>> processException)
        {
            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM ConfigurationEntity")) == 0)
            {
                statusMessage("CommonPhasorServices", new EventArgs<string>("Loading default records for ConfigurationEntity..."));
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('IaonInputAdapter', 'InputAdapters', 'Defines IInputAdapter definitions for a PDC node', 1, 1)");
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('IaonActionAdapter', 'ActionAdapters', 'Defines IActionAdapter definitions for a PDC node', 2, 1)");
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('IaonOutputAdapter', 'OutputAdapters', 'Defines IOutputAdapter definitions for a PDC node', 3, 1)");
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('ActiveMeasurement', 'ActiveMeasurements', 'Defines active system measurements for a PDC node', 4, 1)");
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('RuntimeInputStreamDevice', 'InputStreamDevices', 'Defines input stream devices associated with a concentrator', 5, 1)");
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('RuntimeOutputStreamDevice', 'OutputStreamDevices', 'Defines output stream devices defined for a concentrator', 6, 1)");
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('RuntimeOutputStreamMeasurement', 'OutputStreamMeasurements', 'Defines output stream measurements for an output stream', 7, 1)");
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('OutputStreamDevicePhasor', 'OutputStreamDevicePhasors', 'Defines phasors for output stream devices', 8, 1)");
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('OutputStreamDeviceAnalog', 'OutputStreamDeviceAnalogs', 'Defines analog values for output stream devices', 9, 1)");
                connection.ExecuteNonQuery("INSERT INTO ConfigurationEntity(SourceName, RuntimeName, Description, LoadOrder, Enabled) VALUES('OutputStreamDeviceDigital', 'OutputStreamDeviceDigitals', 'Defines digital values for output stream devices', 10, 1)");
            }
        }

        /// <summary>
        /// Loads the default configuration for the Interconnection table.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="statusMessage">The delegate which will display a status message to the user.</param>
        /// <param name="processException">The delegate which will handle exception logging.</param>
        private static void LoadDefaultInterconnection(IDbConnection connection, Action<object, EventArgs<string>> statusMessage, Action<object, EventArgs<Exception>> processException)
        {
            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM Interconnection")) == 0)
            {
                statusMessage("CommonPhasorServices", new EventArgs<string>("Loading default records for Interconnection..."));
                connection.ExecuteNonQuery("INSERT INTO Interconnection(Acronym, Name, LoadOrder) VALUES('Eastern', 'Eastern Interconnection', 0)");
                connection.ExecuteNonQuery("INSERT INTO Interconnection(Acronym, Name, LoadOrder) VALUES('Western', 'Western Interconnection', 1)");
                connection.ExecuteNonQuery("INSERT INTO Interconnection(Acronym, Name, LoadOrder) VALUES('ERCOT', 'Texas Interconnection', 2)");
                connection.ExecuteNonQuery("INSERT INTO Interconnection(Acronym, Name, LoadOrder) VALUES('Quebec', 'Quebec Interconnection', 3)");
                connection.ExecuteNonQuery("INSERT INTO Interconnection(Acronym, Name, LoadOrder) VALUES('Alaskan', 'Alaskan Interconnection', 4)");
                connection.ExecuteNonQuery("INSERT INTO Interconnection(Acronym, Name, LoadOrder) VALUES('Hawaii', 'Islands of Hawaii', 5)");
            }
        }

        /// <summary>
        /// Loads the default configuration for the Protocol table.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="statusMessage">The delegate which will display a status message to the user.</param>
        /// <param name="processException">The delegate which will handle exception logging.</param>
        private static void LoadDefaultProtocol(IDbConnection connection, Action<object, EventArgs<string>> statusMessage, Action<object, EventArgs<Exception>> processException)
        {
            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM Protocol")) == 0)
            {
                statusMessage("CommonPhasorServices", new EventArgs<string>("Loading default records for Protocol..."));
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('IeeeC37_118V1', 'IEEE C37.118-2005', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 1)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('IeeeC37_118D6', 'IEEE C37.118 Draft 6', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 3)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('Ieee1344', 'IEEE 1344-1995', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 4)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('BpaPdcStream', 'BPA PDCstream', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 5)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('FNet', 'UTK FNET', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 6)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('SelFastMessage', 'SEL Fast Message', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 7)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('Macrodyne', 'Macrodyne', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 8)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('GatewayTransport', 'Gateway Transport', 'Measurement', 'Gateway', 'GSF.TimeSeries.dll', 'GSF.TimeSeries.Transport.DataSubscriber', 9)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('IeeeC37_118V2', 'IEEE C37.118.2-2011', 'Frame', 'Phasor', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.PhasorMeasurementMapper', 2)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('WAV', 'Wave Form Input Adapter', 'Frame', 'Audio', 'WavInputAdapter.dll', 'WavInputAdapter.WavInputAdapter', 10)");
                connection.ExecuteNonQuery("INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('VirtualInput', 'Virtual Device', 'Frame', 'Virtual', 'TestingAdapters.dll', 'TestingAdapters.VirtualInputAdapter', 11)");
            }
        }

        /// <summary>
        /// Loads the default configuration for the SignalType table.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="statusMessage">The delegate which will display a status message to the user.</param>
        /// <param name="processException">The delegate which will handle exception logging.</param>
        private static void LoadDefaultSignalType(IDbConnection connection, Action<object, EventArgs<string>> statusMessage, Action<object, EventArgs<Exception>> processException)
        {
            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM SignalType WHERE Source = 'Phasor' OR Source = 'PMU'")) == 0)
            {
                statusMessage("CommonPhasorServices", new EventArgs<string>("Loading default records for SignalType..."));
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Current Magnitude', 'IPHM', 'PM', 'I', 'Phasor', 'Amps')");
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Current Phase Angle', 'IPHA', 'PA', 'IH', 'Phasor', 'Degrees')");
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Voltage Magnitude', 'VPHM', 'PM', 'V', 'Phasor', 'Volts')");
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Voltage Phase Angle', 'VPHA', 'PA', 'VH', 'Phasor', 'Degrees')");
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Frequency', 'FREQ', 'FQ', 'F', 'PMU', 'Hz')");
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Frequency Delta (dF/dt)', 'DFDT', 'DF', 'DF', 'PMU', '')");
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Analog Value', 'ALOG', 'AV', 'A', 'PMU', '')");
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Status Flags', 'FLAG', 'SF', 'S', 'PMU', '')");
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Digital Value', 'DIGI', 'DV', 'D', 'PMU', '')");
                connection.ExecuteNonQuery("INSERT INTO SignalType(Name, Acronym, Suffix, Abbreviation, Source, EngineeringUnits) VALUES('Calculated Value', 'CALC', 'CV', 'C', 'PMU', '')");
            }
        }

        /// <summary>
        /// Loads the default configuration for the Statistic table.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="statusMessage">The delegate which will display a status message to the user.</param>
        /// <param name="processException">The delegate which will handle exception logging.</param>
        private static void LoadDefaultStatistic(IDbConnection connection, Action<object, EventArgs<string>> statusMessage, Action<object, EventArgs<Exception>> processException)
        {
            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM Statistic")) == 0)
            {
                statusMessage("CommonPhasorServices", new EventArgs<string>("Loading default records for Statistic..."));
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Device', 1, 'Data Quality Errors', 'Number of data quaility errors reported by device during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetDeviceStatistic_DataQualityErrors', '', 1, 'System.Int32', @displayFormat, 0, 1)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Device', 2, 'Time Quality Errors', 'Number of time quality errors reported by device during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetDeviceStatistic_TimeQualityErrors', '', 1, 'System.Int32', @displayFormat, 0, 2)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Device', 3, 'Device Errors', 'Number of device errors reported by device during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetDeviceStatistic_DeviceErrors', '', 1, 'System.Int32', @displayFormat, 0, 3)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 1, 'Total Frames', 'Total number of frames received from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_TotalFrames', '', 1, 'System.Int32', @displayFormat, 0, 2)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 2, 'Last Report Time', 'Timestamp of last received data frame from input stream.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_LastReportTime', '', 1, 'System.DateTime', @displayFormat, 0, 1)", "{0:mm':'ss'.'fff}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 3, 'Missing Frames', 'Number of frames that were not received from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_MissingFrames', '', 1, 'System.Int32', @displayFormat, 0, 3)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 4, 'CRC Errors', 'Number of CRC errors reported from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_CRCErrors', '', 1, 'System.Int32', @displayFormat, 0, 16)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 5, 'Out of Order Frames', 'Number of out-of-order frames received from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_OutOfOrderFrames', '', 1, 'System.Int32', @displayFormat, 0, 17)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 6, 'Minimum Latency', 'Minimum latency from input stream, in milliseconds, during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_MinimumLatency', '', 1, 'System.Double', @displayFormat, 0, 10)", "{0:N3} ms");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 7, 'Maximum Latency', 'Maximum latency from input stream, in milliseconds, during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_MaximumLatency', '', 1, 'System.Double', @displayFormat, 0, 11)", "{0:N3} ms");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 8, 'Input Stream Connected', 'Boolean value representing if input stream was continually connected during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_Connected', '', 1, 'System.Boolean', @displayFormat, 1, 18)", "{0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 9, 'Received Configuration', 'Boolean value representing if input stream has received (or has cached) a configuration frame during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_ReceivedConfiguration', '', 1, 'System.Boolean', @displayFormat, 0, 8)", "{0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 10, 'Configuration Changes', 'Number of configuration changes reported by input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_ConfigurationChanges', '', 1, 'System.Int32', @displayFormat, 0, 9)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 11, 'Total Data Frames', 'Number of data frames received from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_TotalDataFrames', '', 1, 'System.Int32', @displayFormat, 0, 5)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 12, 'Total Configuration Frames', 'Number of configuration frames received from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_TotalConfigurationFrames', '', 1, 'System.Int32', @displayFormat, 0, 6)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 13, 'Total Header Frames', 'Number of header frames received from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_TotalHeaderFrames', '', 1, 'System.Int32', @displayFormat, 0, 7)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 14, 'Average Latency', 'Average latency, in milliseconds, for data received from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_AverageLatency', '', 1, 'System.Double', @displayFormat, 0, 12)", "{0:N3} ms");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 15, 'Defined Frame Rate', 'Frame rate as defined by input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_DefinedFrameRate', '', 1, 'System.Int32', @displayFormat, 0, 13)", "{0:N0} frames / second");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 16, 'Actual Frame Rate', 'Latest actual mean frame rate for data received from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_ActualFrameRate', '', 1, 'System.Double', @displayFormat, 0, 14)", "{0:N3} frames / second");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 17, 'Actual Data Rate', 'Latest actual mean Mbps data rate for data received from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_ActualDataRate', '', 1, 'System.Double', @displayFormat, 0, 15)", "{0:N3} Mbps");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 1, 'Discarded Measurements', 'Number of discarded measurements reported by output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_DiscardedMeasurements', '', 1, 'System.Int32', @displayFormat, 0, 4)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 2, 'Received Measurements', 'Number of received measurements reported by the output strean during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_ReceivedMeasurements', '', 1, 'System.Int32', @displayFormat, 0, 2)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 3, 'Expected Measurements', 'Number of expected measurements reported by the output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_ExpectedMeasurements', '', 1, 'System.Int32', @displayFormat, 0, 1)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 4, 'Processed Measurements', 'Number of processed measurements reported by the output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_ProcessedMeasurements', '', 1, 'System.Int32', @displayFormat, 0, 3)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 5, 'Measurements Sorted by Arrival', 'Number of measurments sorted by arrival reported by the output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_MeasurementsSortedByArrival', '', 1, 'System.Int32', @displayFormat, 0, 7)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 6, 'Published Measurements', 'Number of published measurements reported by output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_PublishedMeasurements', '', 1, 'System.Int32', @displayFormat, 0, 5)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 7, 'Downsampled Measurements', 'Number of downsampled measurements reported by the output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_DownsampledMeasurements', '', 1, 'System.Int32', @displayFormat, 0, 6)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 8, 'Missed Sorts by Timeout', 'Number of missed sorts by timeout reported by the output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_MissedSortsByTimeout', '', 1, 'System.Int32', @displayFormat, 0, 8)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 9, 'Frames Ahead of Schedule', 'Number of frames ahead of schedule reported by the output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_FramesAheadOfSchedule', '', 1, 'System.Int32', @displayFormat, 0, 9)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 10, 'Published Frames', 'Number of published frames reported by the output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_PublishedFrames', '', 1, 'System.Int32', @displayFormat, 0, 10)", "{0:N0}");
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 11, 'Output Stream Connected', 'Boolean value representing if the output stream was continually connected during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_Connected', '', 1, 'System.Boolean', @displayFormat, 1, 11)", "{0}");
            }

            // Make sure new input stream statistics are defined
            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM Statistic WHERE MethodName = 'GetInputStreamStatistic_MissingData'")) == 0)
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('InputStream', 18, 'Missing Data', 'Number of data units that were not received at least once from input stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetInputStreamStatistic_MissingData', '', 1, 'System.Int32', @displayFormat, 0, 4)", "{0:N0}");

            // Make sure new output stream statistics are defined
            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM Statistic WHERE MethodName = 'GetOutputStreamStatistic_MinimumLatency'")) == 0)
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 12, 'Minimum Latency', 'Minimum latency from output stream, in milliseconds, during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_MinimumLatency', '', 1, 'System.Double', @displayFormat, 0, 12)", "{0:N3} ms");

            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM Statistic WHERE MethodName = 'GetOutputStreamStatistic_MaximumLatency'")) == 0)
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 13, 'Maximum Latency', 'Maximum latency from output stream, in milliseconds, during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_MaximumLatency', '', 1, 'System.Double', @displayFormat, 0, 13)", "{0:N3} ms");

            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM Statistic WHERE MethodName = 'GetOutputStreamStatistic_AverageLatency'")) == 0)
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 14, 'Average Latency', 'Average latency, in milliseconds, for data published from output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_AverageLatency', '', 1, 'System.Double', @displayFormat, 0, 14)", "{0:N3} ms");

            if (Convert.ToInt32(connection.ExecuteScalar("SELECT COUNT(*) FROM Statistic WHERE MethodName = 'GetOutputStreamStatistic_ConnectedClientCount'")) == 0)
                LoadStatistic(connection, "INSERT INTO Statistic(Source, SignalIndex, Name, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('OutputStream', 15, 'Connected Clients', 'Number of clients connected to the command channel of the output stream during last reporting interval.', 'PhasorProtocolAdapters.dll', 'PhasorProtocolAdapters.CommonPhasorServices', 'GetOutputStreamStatistic_ConnectedClientCount', '', 1, 'System.Int32', @displayFormat, 0, 15)", "{0:N0}");
        }

        [SuppressMessage("Microsoft.Security", "CA2100")]
        private static void LoadStatistic(IDbConnection connection, string commandText, string displayFormat)
        {
            bool oracle = connection.GetType().Name == "OracleConnection";
            char paramChar = oracle ? ':' : '@';

            using (IDbCommand command = connection.CreateCommand())
            {
                IDbDataParameter parameter = command.CreateParameter();

                parameter.ParameterName = paramChar + "displayFormat";
                parameter.Value = displayFormat;
                parameter.Direction = ParameterDirection.Input;

                command.Parameters.Add(parameter);
                command.CommandText = oracle ? commandText.Replace('@', ':') : commandText;
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Establish default <see cref="MeasurementKey"/> cache.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="adapterType">The database adapter type.</param>
        /// <param name="statusMessage">The delegate which will display a status message to the user.</param>
        /// <param name="processException">The delegate which will handle exception logging.</param>
        [SuppressMessage("Microsoft.Usage", "CA1806")]
        private static void EstablishDefaultMeasurementKeyCache(IDbConnection connection, Type adapterType, Action<object, EventArgs<string>> statusMessage, Action<object, EventArgs<Exception>> processException)
        {
            string keyID;
            string[] elems;

            statusMessage("CommonPhasorServices", new EventArgs<string>("Establishing default measurement key cache..."));

            // Establish default measurement key cache
            foreach (DataRow measurement in connection.RetrieveData(adapterType, "SELECT ID, SignalID FROM ActiveMeasurement").Rows)
            {
                keyID = measurement["ID"].ToNonNullString();

                if (!string.IsNullOrWhiteSpace(keyID))
                {
                    elems = keyID.Split(':');

                    // Cache new measurement key with associated Guid signal ID
                    if (elems.Length == 2)
                        new MeasurementKey(measurement["SignalID"].ToNonNullString(Guid.Empty.ToString()).ConvertToType<Guid>(), uint.Parse(elems[1].Trim()), elems[0].Trim());
                }
            }
        }

        private static string GetDeviceAcronym(object source)
        {
            ConfigurationCell device = source as ConfigurationCell;

            if ((object)device == null)
                return null;

            return device.IDLabel;
        }

        #region [ Device Statistic Calculators ]

        /// <summary>
        /// Calculates number of data quaility errors reported by device during last reporting interval.
        /// </summary>
        /// <param name="source">Source Device.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Data Quality Errors Statistic.</returns>
        private static double GetDeviceStatistic_DataQualityErrors(object source, string arguments)
        {
            double statistic = 0.0D;
            ConfigurationCell device = source as ConfigurationCell;

            if ((object)device != null)
                statistic = s_statisticValueCache.GetDifference(device, device.DataQualityErrors, "DataQualityErrors");

            return statistic;
        }

        /// <summary>
        /// Calculates number of time quality errors reported by device during last reporting interval.
        /// </summary>
        /// <param name="source">Source Device.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Time Quality Errors Statistic.</returns>
        private static double GetDeviceStatistic_TimeQualityErrors(object source, string arguments)
        {
            double statistic = 0.0D;
            ConfigurationCell device = source as ConfigurationCell;

            if ((object)device != null)
                statistic = s_statisticValueCache.GetDifference(device, device.TimeQualityErrors, "TimeQualityErrors");

            return statistic;
        }

        /// <summary>
        /// Calculates number of device errors reported by device during last reporting interval.
        /// </summary>
        /// <param name="source">Source Device.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Device Errros Statistic.</returns>
        private static double GetDeviceStatistic_DeviceErrors(object source, string arguments)
        {
            double statistic = 0.0D;
            ConfigurationCell device = source as ConfigurationCell;

            if ((object)device != null)
                statistic = s_statisticValueCache.GetDifference(device, device.DeviceErrors, "DeviceErrors");

            return statistic;
        }

        #endregion

        #region [ InputStream Statistic Calculators ]

        /// <summary>
        /// Calculates total number of frames received from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <remarks>
        /// This statistic also calculates the other frame count statistics so its load order must occur first.
        /// </remarks>
        /// <returns>Total Frames Statistic.</returns>
        private static double GetInputStreamStatistic_TotalFrames(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = s_statisticValueCache.GetDifference(inputStream, inputStream.TotalFrames, "TotalFrames");

            return statistic;
        }

        /// <summary>
        /// Calculates timestamp of last received data frame from input stream.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Last Report Time Statistic.</returns>
        private static double GetInputStreamStatistic_LastReportTime(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            // Local archival uses a 32-bit floating point number for statistical value storage so we
            // reduce the last reporting time resolution down to the hour to make sure the archived
            // timestamp is accurate at least to the milliseconds - remaining date/time high data bits
            // can be later deduced from the statistic's archival timestamp
            if ((object)inputStream != null)
            {
                Ticks lastReportTime = inputStream.LastReportTime;
                statistic = lastReportTime - lastReportTime.BaselinedTimestamp(BaselineTimeInterval.Hour);
            }

            return statistic;
        }

        /// <summary>
        /// Calculates number of frames that were not received from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Missing Frames Statistic.</returns>
        private static double GetInputStreamStatistic_MissingFrames(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = s_statisticValueCache.GetDifference(inputStream, inputStream.MissingFrames, "MissingFrames");

            return statistic;
        }

        /// <summary>
        /// Calculates number of data units that were not received at least once from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Missing Data Statistic.</returns>
        private static double GetInputStreamStatistic_MissingData(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = s_statisticValueCache.GetDifference(inputStream, inputStream.MissingData, "MissingData");

            return statistic;
        }

        /// <summary>
        /// Calculates number of CRC errors reported from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>CRC Errors Statistic.</returns>
        private static double GetInputStreamStatistic_CRCErrors(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = s_statisticValueCache.GetDifference(inputStream, inputStream.CRCErrors, "CRCErrors");

            return statistic;
        }

        /// <summary>
        /// Calculates number of out-of-order frames received from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Out of Order Frames Statistic.</returns>
        private static double GetInputStreamStatistic_OutOfOrderFrames(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = s_statisticValueCache.GetDifference(inputStream, inputStream.OutOfOrderFrames, "OutOfOrderFrames");

            return statistic;
        }

        /// <summary>
        /// Calculates minimum latency from input stream, in milliseconds, during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <remarks>
        /// This statistic also calculates the maximum and average latency statistics so its load order must occur first.
        /// </remarks>
        /// <returns>Minimum Latency Statistic.</returns>
        private static double GetInputStreamStatistic_MinimumLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.MinimumLatency;

            return statistic;
        }

        /// <summary>
        /// Calculates maximum latency from input stream, in milliseconds, during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Maximum Latency Statistic.</returns>
        private static double GetInputStreamStatistic_MaximumLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.MaximumLatency;

            return statistic;
        }

        /// <summary>
        /// Calculates average latency, in milliseconds, for data received from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Average Latency Statistic.</returns>
        private static double GetInputStreamStatistic_AverageLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.AverageLatency;

            return statistic;
        }

        /// <summary>
        /// Calculates boolean value representing if input stream was continually connected during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Input Stream Connected Statistic.</returns>
        private static double GetInputStreamStatistic_Connected(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
            {
                if (inputStream.IsConnected)
                    statistic = (s_statisticValueCache.GetDifference(inputStream, inputStream.ConnectionAttempts, "ConnectionAttempts") == 0.0D ? 1.0D : 0.0D);
            }

            return statistic;
        }

        /// <summary>
        /// Calculates boolean value representing if input stream has received (or has cached) a configuration frame during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <remarks>
        /// This statistic also calculates the total configuration changes so its load order must occur first.
        /// </remarks>
        /// <returns>Received Configuration Statistic.</returns>
        private static double GetInputStreamStatistic_ReceivedConfiguration(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
            {
                double configChanges = s_statisticValueCache.GetDifference(inputStream, inputStream.ConfigurationChanges, "ReceivedConfiguration");
                statistic = (configChanges > 0 ? 1.0D : 0.0D);
            }

            return statistic;
        }

        /// <summary>
        /// Calculates number of configuration changes reported by input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Configuration Changes Statistic.</returns>
        private static double GetInputStreamStatistic_ConfigurationChanges(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = (long)s_statisticValueCache.GetDifference(inputStream, inputStream.ConfigurationChanges, "ConfigurationChanges");

            return statistic;
        }

        /// <summary>
        /// Calculates number of data frames received from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Total Data Frames Statistic.</returns>
        private static double GetInputStreamStatistic_TotalDataFrames(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = s_statisticValueCache.GetDifference(inputStream, inputStream.TotalDataFrames, "TotalDataFrames");

            return statistic;
        }

        /// <summary>
        /// Calculates number of configuration frames received from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Total Configuration Frames Statistic.</returns>
        private static double GetInputStreamStatistic_TotalConfigurationFrames(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = s_statisticValueCache.GetDifference(inputStream, inputStream.TotalConfigurationFrames, "TotalConfigurationFrames");

            return statistic;
        }

        /// <summary>
        /// Calculates number of header frames received from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Total Header Frames Statistic.</returns>
        private static double GetInputStreamStatistic_TotalHeaderFrames(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = s_statisticValueCache.GetDifference(inputStream, inputStream.TotalHeaderFrames, "TotalHeaderFrames");

            return statistic;
        }

        /// <summary>
        /// Calculates frame rate as defined by input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Defined Frame Rate Statistic.</returns>
        private static double GetInputStreamStatistic_DefinedFrameRate(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.DefinedFrameRate;

            return statistic;
        }

        /// <summary>
        /// Calculates latest actual mean frame rate for data received from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Actual Frame Rate Statistic.</returns>
        private static double GetInputStreamStatistic_ActualFrameRate(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.ActualFrameRate;

            return statistic;
        }

        /// <summary>
        /// Calculates latest actual mean Mbps data rate for data received from input stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source InputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Actual Data Rate Statistic.</returns>
        private static double GetInputStreamStatistic_ActualDataRate(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.ActualDataRate * 8.0D / SI.Mega;

            return statistic;
        }

        private static double GetInputStreamStatistic_TotalBytesReceived(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
            {
                statistic = s_statisticValueCache.GetDifference(source, inputStream.TotalBytesReceived, "TotalBytesReceived");

                if (statistic < 0.0D)
                    statistic = inputStream.TotalBytesReceived;
            }

            return statistic;
        }

        private static double GetInputStreamStatistic_LifetimeMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.LifetimeMeasurements;

            return statistic;
        }

        private static double GetInputStreamStatistic_MinimumMeasurementsPerSecond(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.MinimumMeasurementsPerSecond;

            return statistic;
        }

        private static double GetInputStreamStatistic_MaximumMeasurementsPerSecond(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.MaximumMeasurementsPerSecond;

            return statistic;
        }

        private static double GetInputStreamStatistic_AverageMeasurementsPerSecond(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.AverageMeasurementsPerSecond;

            return statistic;
        }

        private static double GetInputStreamStatistic_LifetimeBytesReceived(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.TotalBytesReceived;

            return statistic;
        }

        private static double GetInputStreamStatistic_LifetimeMinimumLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.LifetimeMinimumLatency;

            return statistic;
        }

        private static double GetInputStreamStatistic_LifetimeMaximumLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.LifetimeMaximumLatency;

            return statistic;
        }

        private static double GetInputStreamStatistic_LifetimeAverageLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorMeasurementMapper inputStream = source as PhasorMeasurementMapper;

            if ((object)inputStream != null)
                statistic = inputStream.LifetimeAverageLatency;

            return statistic;
        }

        #endregion

        #region [ OutputStream Statistic Calculators ]

        /// <summary>
        /// Calculates number of discarded measurements reported by output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Discarded Measurements Statistic.</returns>
        private static double GetOutputStreamStatistic_DiscardedMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.DiscardedMeasurements, "DiscardedMeasurements");

            return statistic;
        }

        /// <summary>
        /// Calculates number of received measurements reported by the output strean during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Received Measurements Statistic.</returns>
        private static double GetOutputStreamStatistic_ReceivedMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.ReceivedMeasurements, "ReceivedMeasurements");

            return statistic;
        }

        /// <summary>
        /// Calculates number of expected measurements reported by the output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <remarks>
        /// This statistic also calculates the total published frame count statistic so its load order must occur first.
        /// </remarks>
        /// <returns>Expected Measurements Statistic.</returns>
        private static double GetOutputStreamStatistic_ExpectedMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
            {
                double publishedFrames = s_statisticValueCache.GetDifference(outputStream, outputStream.PublishedFrames, "ExpectedMeasurements");
                statistic = outputStream.ExpectedMeasurements * publishedFrames;
            }

            return statistic;
        }

        /// <summary>
        /// Calculates number of processed measurements reported by the output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Processed Measurements Statistic.</returns>
        private static double GetOutputStreamStatistic_ProcessedMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.ProcessedMeasurements, "ProcessedMeasurements");

            return statistic;
        }

        /// <summary>
        /// Calculates number of measurments sorted by arrival reported by the output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Measurements Sorted by Arrival Statistic.</returns>
        private static double GetOutputStreamStatistic_MeasurementsSortedByArrival(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.MeasurementsSortedByArrival, "MeasurementsSortedByArrival");

            return statistic;
        }

        /// <summary>
        /// Calculates number of published measurements reported by output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Published Measurements Statistic.</returns>
        private static double GetOutputStreamStatistic_PublishedMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.PublishedMeasurements, "PublishedMeasurements");

            return statistic;
        }

        /// <summary>
        /// Calculates number of downsampled measurements reported by the output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Downsampled Measurements Statistic.</returns>
        private static double GetOutputStreamStatistic_DownsampledMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.DownsampledMeasurements, "DownsampledMeasurements");

            return statistic;
        }

        /// <summary>
        /// Calculates number of missed sorts by timeout reported by the output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Missed Sorts by Timeout Statistic.</returns>
        private static double GetOutputStreamStatistic_MissedSortsByTimeout(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.MissedSortsByTimeout, "MissedSortsByTimeout");

            return statistic;
        }

        /// <summary>
        /// Calculates number of frames ahead of schedule reported by the output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Frames Ahead of Schedule Statistic.</returns>
        private static double GetOutputStreamStatistic_FramesAheadOfSchedule(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.FramesAheadOfSchedule, "FramesAheadOfSchedule");

            return statistic;
        }

        /// <summary>
        /// Calculates number of published frames reported by the output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Published Frames Statistic.</returns>
        private static double GetOutputStreamStatistic_PublishedFrames(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.PublishedFrames, "PublishedFrames");

            return statistic;
        }

        /// <summary>
        /// Calculates boolean value representing if the output stream was continually connected during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Output Stream Connected Statistic.</returns>
        private static double GetOutputStreamStatistic_Connected(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
            {
                if (outputStream.Enabled)
                    statistic = (s_statisticValueCache.GetDifference(outputStream, outputStream.ActiveConnections, "ActiveConnections") == 0.0D ? 1.0D : 0.0D);
            }

            return statistic;
        }

        /// <summary>
        /// Calculates minimum latency from output stream, in milliseconds, during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <remarks>
        /// This statistic also calculates the maximum and average latency statistics so its load order must occur first.
        /// </remarks>
        /// <returns>Minimum Output Latency Statistic.</returns>
        private static double GetOutputStreamStatistic_MinimumLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.MinimumLatency;

            return statistic;
        }

        /// <summary>
        /// Calculates maximum latency from output stream, in milliseconds, during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Maximum Output Latency Statistic.</returns>
        private static double GetOutputStreamStatistic_MaximumLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.MaximumLatency;

            return statistic;
        }

        /// <summary>
        /// Calculates average latency, in milliseconds, for data received from output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Average Output Latency Statistic.</returns>
        private static double GetOutputStreamStatistic_AverageLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.AverageLatency;

            return statistic;
        }

        /// <summary>
        /// Calculates number of clients connected to the command channel of the output stream during last reporting interval.
        /// </summary>
        /// <param name="source">Source OutputStream.</param>
        /// <param name="arguments">Any needed arguments for statistic calculation.</param>
        /// <returns>Output Stream Connected Statistic.</returns>
        private static double GetOutputStreamStatistic_ConnectedClientCount(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.ConnectedClientCount;

            return statistic;
        }

        private static double GetOutputStreamStatistic_TotalBytesSent(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
            {
                statistic = s_statisticValueCache.GetDifference(outputStream, outputStream.TotalBytesSent, "TotalBytesSent");

                if (statistic < 0.0D)
                    statistic = outputStream.TotalBytesSent;
            }

            return statistic;
        }

        private static double GetOutputStreamStatistic_LifetimeMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.LifetimeMeasurements;

            return statistic;
        }

        private static double GetOutputStreamStatistic_MinimumMeasurementsPerSecond(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.MinimumMeasurementsPerSecond;

            return statistic;
        }

        private static double GetOutputStreamStatistic_MaximumMeasurementsPerSecond(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.MaximumMeasurementsPerSecond;

            return statistic;
        }

        private static double GetOutputStreamStatistic_AverageMeasurementsPerSecond(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.AverageMeasurementsPerSecond;

            return statistic;
        }

        private static double GetOutputStreamStatistic_LifetimeBytesSent(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.TotalBytesSent;

            return statistic;
        }

        private static double GetOutputStreamStatistic_LifetimeMinimumLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.LifetimeMinimumLatency;

            return statistic;
        }

        private static double GetOutputStreamStatistic_LifetimeMaximumLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.LifetimeMaximumLatency;

            return statistic;
        }

        private static double GetOutputStreamStatistic_LifetimeAverageLatency(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.LifetimeAverageLatency;

            return statistic;
        }

        private static double GetOutputStreamStatistic_LifetimeDiscardedMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.DiscardedMeasurements;

            return statistic;
        }

        private static double GetOutputStreamStatistic_LifetimeDownsampledMeasurements(object source, string arguments)
        {
            double statistic = 0.0D;
            PhasorDataConcentratorBase outputStream = source as PhasorDataConcentratorBase;

            if ((object)outputStream != null)
                statistic = outputStream.DownsampledMeasurements;

            return statistic;
        }

        #endregion

        #endregion
    }
}
