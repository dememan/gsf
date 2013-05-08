﻿//******************************************************************************************************
//  Dnp3InputAdapter.cs - Gbtc
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
//  10/05/2012 - Adam Crain
//       Generated original version of source code.
//  12/13/2012 - Starlynn Danyelle Gilliam
//       Modified Header. 
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using DNP3.Adapter;
using DNP3.Interface;
using GSF.TimeSeries;
using GSF.TimeSeries.Adapters;

namespace Dnp3Adapters
{
    /// <summary>
    /// DNP3 Adapter
    /// </summary>
    [Description("DNP3: Reads measurements from a remote dnp3 endpoint")]
    public class Dnp3InputAdapter : InputAdapterBase
    {
        /// <summary>
        /// DNP3 manager shared across all of the DNP3 input adapters
        /// </summary>
        private static readonly StackManager m_Manager = new StackManager();

        /// <summary>
        /// The filename for the communication configuration file
        /// </summary>
        private String m_commsFileName;

        /// <summary>
        /// The filename for the measurement mapping configuration file
        /// </summary>
        private String m_mappingFileName;

        /// <summary>
        /// Configuration for the master. Set during the Initialize call.
        /// </summary>
        private MasterConfiguration m_MasterConfig;

        /// <summary>
        /// Configuration for the measurement map. Set during the Initialize call.
        /// </summary>
        private MeasurementMap m_MeasMap;

        /// <summary>
        /// Gets set during the AttemptConnection() call and using during AttemptDisconnect()
        /// </summary>
        private String m_portName;

        /// <summary>
        /// A counter of the number of measurements received
        /// </summary>
        private int m_numMeasurementsReceived;

        /// <summary>
        /// Flag that records wether or not the port/master have been added so that the resource can be cleaned
        /// up in the Dispose/AttemptDisconnect methods
        /// </summary>
        private bool m_active;

        /// <summary>
        /// Gets or sets the name of the xml file from which the communication parameters will be read
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the name of the XML file from which the communication configuration will be read"),
        DefaultValue("comms1.xml")]
        public string CommsFileName
        {
            get
            {
                return m_commsFileName;
            }
            set
            {
                m_commsFileName = value;
            }
        }

        /// <summary>
        /// Gets or sets the name of the xml file from which the measurement mapping is read
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the name of the XML file from which the communication configuration will be read"),
        DefaultValue("device1.xml")]
        public string MappingFileName
        {
            get
            {
                return m_mappingFileName;
            }
            set
            {
                m_mappingFileName = value;
            }
        }

       /// <summary>
       /// Initializes <see cref="Dnp3InputAdapter"/>
       /// </summary>
        public override void Initialize()
        {
            base.Initialize();
                        
            this.Settings.TryGetValue("commsFile", out this.m_commsFileName);
            this.Settings.TryGetValue("mappingFile", out this.m_mappingFileName);                                  

            try
            {
                if (this.m_commsFileName == null) throw new ArgumentException("The required commsFile parameter was not specified");
                if (this.m_mappingFileName == null) throw new ArgumentException("The required mappingFile parameter was not specified");

                this.m_MasterConfig = this.ReadConfig<MasterConfiguration>(this.CommsFileName);
                this.m_MeasMap = this.ReadConfig<MeasurementMap>(this.MappingFileName);                
            }
            catch (Exception ex)
            {                
                this.OnProcessException(ex);                
            }            
        }

        /// <summary>
        /// Disposes the <see cref="Dnp3InputAdapter"/>
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing</param>
        protected override void Dispose(bool disposing)
        {
            if (this.m_active)
            {
                m_Manager.RemovePort(m_portName);
                this.m_active = false;
            }
            base.Dispose(disposing);
        }

        private T ReadConfig<T>(string path)
        {
            var ser = new XmlSerializer(typeof(T));
            var stream = new StreamReader(path);
            try
            {
                return (T)ser.Deserialize(stream);
            }
            finally
            {
                stream.Close();
            }
        }
        
        /// <summary>
        /// Async connection flag
        /// </summary>
        protected override bool UseAsyncConnect
        {
            get { return false; }
        }

        /// <summary>
        /// Connection attempt method
        /// </summary>
        protected override void AttemptConnection()
        {            
            var tcp = this.m_MasterConfig.client;
            var master = this.m_MasterConfig.master;
            this.m_portName = tcp.address + ":" + tcp.port;
            m_Manager.AddTCPClient(m_portName, tcp.level, tcp.retryMs, tcp.address, tcp.port);            
            var adapter = new TsfDataObserver(new MeasurementLookup(this.m_MeasMap));
            adapter.NewMeasurements += adapter_NewMeasurements;
            adapter.NewMeasurements += this.OnNewMeasurements;           
            var acceptor = m_Manager.AddMaster(m_portName, this.Name, FilterLevel.LEV_WARNING, adapter, m_MasterConfig.master);
            this.m_active = true;
        }

        void adapter_NewMeasurements(ICollection<IMeasurement> measurements)
        {
            this.m_numMeasurementsReceived += measurements.Count;                      
        }        

        /// <summary>
        /// Disconnect attempt method
        /// </summary>
        protected override void AttemptDisconnection()
        {
            if (this.m_active)
            {                
                m_Manager.RemovePort(m_portName);
                this.m_active = false;
            }
        }

        /// <summary>
        /// Temporal support flag
        /// </summary>
        public override bool SupportsTemporalProcessing
        {
            get { return false; }
        }

        /// <summary>
        /// Short status of adapter
        /// </summary>
        /// <param name="maxLength">Maximum length of status message</param>
        /// <returns>Short status of adapter</returns>
        public override string GetShortStatus(int maxLength)
        {
            return "The adapter has received " + this.m_numMeasurementsReceived + " measurements";
        }
    }    
    
}
