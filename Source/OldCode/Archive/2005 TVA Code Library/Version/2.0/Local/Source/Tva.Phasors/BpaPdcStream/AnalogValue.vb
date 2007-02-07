'*******************************************************************************************************
'  AnalogValue.vb - PDCstream Analog value
'  Copyright � 2005 - TVA, all rights reserved - Gbtc
'
'  Build Environment: VB.NET, Visual Studio 2005
'  Primary Developer: J. Ritchie Carroll, Operations Data Architecture [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  -----------------------------------------------------------------------------------------------------
'  11/12/2004 - J. Ritchie Carroll
'       Initial version of source generated
'
'*******************************************************************************************************

Imports System.Runtime.Serialization

Namespace BpaPdcStream

    <CLSCompliant(False), Serializable()> _
    Public Class AnalogValue

        Inherits AnalogValueBase

        Protected Sub New()
        End Sub

        Protected Sub New(ByVal info As SerializationInfo, ByVal context As StreamingContext)

            MyBase.New(info, context)

        End Sub

        Public Sub New(ByVal parent As IDataCell, ByVal analogDefinition As IAnalogDefinition, ByVal value As Single)

            MyBase.New(parent, analogDefinition, value)

        End Sub

        Public Sub New(ByVal parent As IDataCell, ByVal analogDefinition As IAnalogDefinition, ByVal unscaledValue As Int16)

            MyBase.New(parent, analogDefinition, unscaledValue)

        End Sub

        Public Sub New(ByVal parent As IDataCell, ByVal analogDefinition As IAnalogDefinition, ByVal binaryImage As Byte(), ByVal startIndex As Int32)

            MyBase.New(parent, analogDefinition, binaryImage, startIndex)

        End Sub

        Public Sub New(ByVal analogValue As IAnalogValue)

            MyBase.New(analogValue)

        End Sub

        Public Overrides ReadOnly Property DerivedType() As System.Type
            Get
                Return Me.GetType
            End Get
        End Property

    End Class

End Namespace