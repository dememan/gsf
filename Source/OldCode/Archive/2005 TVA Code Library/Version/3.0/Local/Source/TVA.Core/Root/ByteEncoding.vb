'*******************************************************************************************************
'  TVA.ByteEncoding.vb - Byte encoding functions
'  Copyright � 2006 - TVA, all rights reserved - Gbtc
'
'  Build Environment: VB.NET, Visual Studio 2005
'  Primary Developer: Pinal C. Patel, Operations Data Architecture [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2250
'       Email: pcpatel@tva.gov
'
'  Code Modification History:
'  -----------------------------------------------------------------------------------------------------
'  04/29/2005 - Pinal C. Patel
'       Generated original version of source code.
'  04/05/2006 - J. Ritchie Carroll
'       Transformed code into System.Text.Encoding styled hierarchy.
'  02/13/2007 - J. Ritchie Carroll
'       Added ASCII character extraction as an encoding option.
'  04/30/2007 - J. Ritchie Carroll
'       Cached binary bit images when first called to optimize generation.
'  12/13/2007 - Darrell Zuercher
'       Edited code comments.
'
'*******************************************************************************************************

Imports System.Text
Imports System.Text.RegularExpressions
Imports System.ComponentModel
Imports TVA.Common
Imports TVA.Interop
Imports TVA.Interop.Bit

''' <summary>Handles conversion of byte buffers to and from user presentable data formats.</summary>
Public MustInherit Class ByteEncoding

#Region " Hexadecimal Encoding Class "

    <EditorBrowsable(EditorBrowsableState.Never)> _
    Public Class HexadecimalEncoding

        Inherits ByteEncoding

        Friend Sub New()

            ' This class is meant for internal instatiation only.

        End Sub

        ''' <summary>Decodes given string back into a byte buffer.</summary>
        ''' <param name="hexData">Encoded hexadecimal data string to decode.</param>
        ''' <param name="spacingCharacter">Original spacing character that was inserted between encoded bytes.</param>
        ''' <returns>Decoded bytes.</returns>
        Public Overrides Function GetBytes(ByVal hexData As String, ByVal spacingCharacter As Char) As Byte()

            If Not String.IsNullOrEmpty(hexData) Then
                ' Removes spacing characters, if needed.
                hexData = hexData.Trim()
                If spacingCharacter <> NoSpacing Then hexData = hexData.Replace(spacingCharacter, "")

                ' Processes the string only if it has data in hex format (Example: 48 65 6C 6C 21).
                If Regex.Matches(hexData, "[^a-fA-F0-9]").Count = 0 Then
                    ' Trims the end of the string to discard any additional characters, if present in the string, that
                    ' would prevent the string from being a hex encoded string. 
                    ' Note: Requires that each character be represented by its 2 character hex value.
                    hexData = hexData.Substring(0, hexData.Length - hexData.Length Mod 2)

                    Dim bytes As Byte() = CreateArray(Of Byte)(hexData.Length \ 2)
                    Dim index As Integer

                    For x As Integer = 0 To hexData.Length - 1 Step 2
                        bytes(index) = Convert.ToByte(hexData.Substring(x, 2), 16)
                        index += 1
                    Next

                    Return bytes
                Else
                    Throw New ArgumentException("Input string is not a valid hex encoded string - invalid characters encountered", "hexData")
                End If
            Else
                Throw New ArgumentNullException("hexData", "Input string cannot be null or empty")
            End If

        End Function

        ''' <summary>Encodes given buffer into a user presentable representation.</summary>
        ''' <param name="bytes">Bytes to encode.</param>
        ''' <param name="offset">Offset into buffer to begin encoding.</param>
        ''' <param name="length">Length of buffer to encode.</param>
        ''' <param name="spacingCharacter">Spacing character to place between encoded bytes.</param>
        ''' <returns>String of encoded bytes.</returns>
        Public Overrides Function GetString(ByVal bytes() As Byte, ByVal offset As Integer, ByVal length As Integer, ByVal spacingCharacter As Char) As String

            Return BytesToString(bytes, offset, length, spacingCharacter, "X2")

        End Function

    End Class

#End Region

#Region " Decimal Encoding Class "

    <EditorBrowsable(EditorBrowsableState.Never)> _
    Public Class DecimalEncoding

        Inherits ByteEncoding

        Friend Sub New()

            ' This class is meant for internal instatiation only.

        End Sub

        ''' <summary>Decodes given string back into a byte buffer.</summary>
        ''' <param name="decData">Encoded decimal data string to decode.</param>
        ''' <param name="spacingCharacter">Original spacing character that was inserted between encoded bytes.</param>
        ''' <returns>Decoded bytes.</returns>
        Public Overrides Function GetBytes(ByVal decData As String, ByVal spacingCharacter As Char) As Byte()

            If Not String.IsNullOrEmpty(decData) Then
                ' Removes spacing characters, if needed.
                decData = decData.Trim()
                If spacingCharacter <> NoSpacing Then decData = decData.Replace(spacingCharacter, "")

                ' Processes the string only if it has data in decimal format (Example: 072 101 108 108 033).
                If Regex.Matches(decData, "[^0-9]").Count = 0 Then
                    ' Trims the end of the string to discard any additional characters, if present in the 
                    ' string, that would prevent the string from being an integer encoded string. 
                    ' Note: Requires that each character be represented by its 3 character decimal value.
                    decData = decData.Substring(0, decData.Length - decData.Length Mod 3)

                    Dim bytes As Byte() = CreateArray(Of Byte)(decData.Length \ 3)
                    Dim index As Integer

                    For x As Integer = 0 To decData.Length - 1 Step 3
                        bytes(index) = Convert.ToByte(decData.Substring(x, 3), 10)
                        index += 1
                    Next

                    Return bytes
                Else
                    Throw New ArgumentException("Input string is not a valid decimal encoded string - invalid characters encountered", "decData")
                End If
            Else
                Throw New ArgumentNullException("decData", "Input string cannot be null or empty")
            End If

        End Function

        ''' <summary>Encodes given buffer into a user presentable representation.</summary>
        ''' <param name="bytes">Bytes to encode.</param>
        ''' <param name="offset">Offset into buffer to begin encoding.</param>
        ''' <param name="length">Length of buffer to encode.</param>
        ''' <param name="spacingCharacter">Spacing character to place between encoded bytes.</param>
        ''' <returns>String of encoded bytes.</returns>
        Public Overrides Function GetString(ByVal bytes() As Byte, ByVal offset As Integer, ByVal length As Integer, ByVal spacingCharacter As Char) As String

            Return BytesToString(bytes, offset, length, spacingCharacter, "D3")

        End Function

    End Class

#End Region

#Region " Binary Encoding Class "

    <EditorBrowsable(EditorBrowsableState.Never)> _
    Public Class BinaryEncoding

        Inherits ByteEncoding

        Private m_byteImages As String()
        Private m_reverse As Boolean

        ' This class is meant for internal instantiation only.
        Friend Sub New(ByVal targetEndianness As Endianness)

            If targetEndianness = Endianness.BigEndian Then
                If BitConverter.IsLittleEndian Then
                    ' If OS is little endian and we want big endian, this reverses the bit order.
                    m_reverse = True
                Else
                    ' If OS is big endian and we want big endian, this keeps the OS bit order.
                    m_reverse = False
                End If
            Else
                If BitConverter.IsLittleEndian Then
                    ' If OS is little endian and we want little endian, this keeps OS bit order.
                    m_reverse = False
                Else
                    ' If OS is big endian and we want little endian, this reverses the bit order.
                    m_reverse = True
                End If
            End If

        End Sub

        ''' <summary>Decodes given string back into a byte buffer.</summary>
        ''' <param name="binaryData">Encoded binary data string to decode.</param>
        ''' <param name="spacingCharacter">Original spacing character that was inserted between encoded bytes.</param>
        ''' <returns>Decoded bytes.</returns>
        Public Overrides Function GetBytes(ByVal binaryData As String, ByVal spacingCharacter As Char) As Byte()

            If Not String.IsNullOrEmpty(binaryData) Then
                ' Removes spacing characters, if needed.
                binaryData = binaryData.Trim
                If spacingCharacter <> NoSpacing Then binaryData = binaryData.Replace(spacingCharacter, "")

                ' Processes the string only if it has data in binary format (Example: 01010110 1010101).
                If Regex.Matches(binaryData, "[^0-1]").Count = 0 Then
                    ' Trims the end of the string to discard any additional characters, if present in the 
                    ' string, that would prevent the string from being a binary encoded string. 
                    ' Note: Requires each character be represented by its 8 character binary value.
                    binaryData = binaryData.Substring(0, binaryData.Length - binaryData.Length Mod 8)

                    Dim bytes As Byte() = CreateArray(Of Byte)(binaryData.Length \ 8)
                    Dim index As Integer

                    For x As Integer = 0 To binaryData.Length - 1 Step 8
                        bytes(index) = Nill

                        If m_reverse Then
                            If binaryData(x + 7) = "1"c Then bytes(index) = (bytes(index) Or Bit0)
                            If binaryData(x + 6) = "1"c Then bytes(index) = (bytes(index) Or Bit1)
                            If binaryData(x + 5) = "1"c Then bytes(index) = (bytes(index) Or Bit2)
                            If binaryData(x + 4) = "1"c Then bytes(index) = (bytes(index) Or Bit3)
                            If binaryData(x + 3) = "1"c Then bytes(index) = (bytes(index) Or Bit4)
                            If binaryData(x + 2) = "1"c Then bytes(index) = (bytes(index) Or Bit5)
                            If binaryData(x + 1) = "1"c Then bytes(index) = (bytes(index) Or Bit6)
                            If binaryData(x + 0) = "1"c Then bytes(index) = (bytes(index) Or Bit7)
                        Else
                            If binaryData(x + 0) = "1"c Then bytes(index) = (bytes(index) Or Bit0)
                            If binaryData(x + 1) = "1"c Then bytes(index) = (bytes(index) Or Bit1)
                            If binaryData(x + 2) = "1"c Then bytes(index) = (bytes(index) Or Bit2)
                            If binaryData(x + 3) = "1"c Then bytes(index) = (bytes(index) Or Bit3)
                            If binaryData(x + 4) = "1"c Then bytes(index) = (bytes(index) Or Bit4)
                            If binaryData(x + 5) = "1"c Then bytes(index) = (bytes(index) Or Bit5)
                            If binaryData(x + 6) = "1"c Then bytes(index) = (bytes(index) Or Bit6)
                            If binaryData(x + 7) = "1"c Then bytes(index) = (bytes(index) Or Bit7)
                        End If

                        index += 1
                    Next

                    Return bytes
                Else
                    Throw New ArgumentException("Input string is not a valid binary encoded string - invalid characters encountered", "binaryData")
                End If
            Else
                Throw New ArgumentNullException("binaryData", "Input string cannot be null or empty")
            End If

        End Function

        ''' <summary>Encodes given buffer into a user presentable representation.</summary>
        ''' <param name="bytes">Bytes to encode.</param>
        ''' <param name="offset">Offset into buffer to begin encoding.</param>
        ''' <param name="length">Length of buffer to encode.</param>
        ''' <param name="spacingCharacter">Spacing character to place between encoded bytes.</param>
        ''' <returns>String of encoded bytes.</returns>
        Public Overrides Function GetString(ByVal bytes() As Byte, ByVal offset As Integer, ByVal length As Integer, ByVal spacingCharacter As Char) As String

            ' Initializes byte image array on first call for speed in future calls.
            If m_byteImages Is Nothing Then
                m_byteImages = CreateArray(Of String)(256)

                For imageByte As Integer = Byte.MinValue To Byte.MaxValue
                    With New StringBuilder
                        If m_reverse Then
                            If (imageByte And Bit7) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit6) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit5) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit4) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit3) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit2) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit1) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit0) > 0 Then .Append("1"c) Else .Append("0"c)
                        Else
                            If (imageByte And Bit0) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit1) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit2) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit3) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit4) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit5) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit6) > 0 Then .Append("1"c) Else .Append("0"c)
                            If (imageByte And Bit7) > 0 Then .Append("1"c) Else .Append("0"c)
                        End If

                        m_byteImages(imageByte) = .ToString()
                    End With
                Next
            End If

            With New StringBuilder
                If bytes IsNot Nothing Then
                    For x As Integer = 0 To length - 1
                        If spacingCharacter <> NoSpacing AndAlso x > 0 Then .Append(spacingCharacter)
                        .Append(m_byteImages(bytes(offset + x)))
                    Next
                End If

                Return .ToString()
            End With

        End Function

    End Class

#End Region

#Region " Base64 Encoding Class "

    <EditorBrowsable(EditorBrowsableState.Never)> _
    Public Class Base64Encoding

        Inherits ByteEncoding

        Friend Sub New()

            ' This class is meant for internal instantiation only.

        End Sub

        ''' <summary>Decodes given string back into a byte buffer.</summary>
        ''' <param name="binaryData">Encoded binary data string to decode.</param>
        ''' <param name="spacingCharacter">Original spacing character that was inserted between encoded bytes.</param>
        ''' <returns>Decoded bytes.</returns>
        Public Overrides Function GetBytes(ByVal binaryData As String, ByVal spacingCharacter As Char) As Byte()

            ' Removes spacing characters, if needed.
            binaryData = binaryData.Trim
            If spacingCharacter <> NoSpacing Then binaryData = binaryData.Replace(spacingCharacter, "")

            Return Convert.FromBase64String(binaryData)

        End Function

        ''' <summary>Encodes given buffer into a user presentable representation.</summary>
        ''' <param name="bytes">Bytes to encode.</param>
        ''' <param name="offset">Offset into buffer to begin encoding.</param>
        ''' <param name="length">Length of buffer to encode.</param>
        ''' <param name="spacingCharacter">Spacing character to place between encoded bytes.</param>
        ''' <returns>String of encoded bytes.</returns>
        Public Overrides Function GetString(ByVal bytes() As Byte, ByVal offset As Integer, ByVal length As Integer, ByVal spacingCharacter As Char) As String

            If spacingCharacter = NoSpacing Then
                Return Convert.ToBase64String(bytes, offset, length)
            Else
                With New StringBuilder
                    If bytes IsNot Nothing Then
                        Dim base64String As String = Convert.ToBase64String(bytes, offset, length)

                        For x As Integer = 0 To base64String.Length - 1
                            If x > 0 Then .Append(spacingCharacter)
                            .Append(base64String(x))
                        Next
                    End If

                    Return .ToString()
                End With
            End If

        End Function

    End Class

#End Region

#Region " ASCII Encoding Class "

    <EditorBrowsable(EditorBrowsableState.Never)> _
    Public Class ASCIIEncoding

        Inherits ByteEncoding

        Friend Sub New()

            ' This class is meant for internal instantiation only.

        End Sub

        ''' <summary>Decodes given string back into a byte buffer.</summary>
        ''' <param name="binaryData">Encoded binary data string to decode.</param>
        ''' <param name="spacingCharacter">Original spacing character that was inserted between encoded bytes.</param>
        ''' <returns>Decoded bytes.</returns>
        Public Overrides Function GetBytes(ByVal binaryData As String, ByVal spacingCharacter As Char) As Byte()

            ' Removes spacing characters, if needed.
            binaryData = binaryData.Trim
            If spacingCharacter <> NoSpacing Then binaryData = binaryData.Replace(spacingCharacter, "")

            Return Encoding.ASCII.GetBytes(binaryData)

        End Function

        ''' <summary>Encodes given buffer into a user presentable representation.</summary>
        ''' <param name="bytes">Bytes to encode.</param>
        ''' <param name="offset">Offset into buffer to begin encoding.</param>
        ''' <param name="length">Length of buffer to encode.</param>
        ''' <param name="spacingCharacter">Spacing character to place between encoded bytes.</param>
        ''' <returns>String of encoded bytes.</returns>
        Public Overrides Function GetString(ByVal bytes() As Byte, ByVal offset As Integer, ByVal length As Integer, ByVal spacingCharacter As Char) As String

            If spacingCharacter = NoSpacing Then
                Return Encoding.ASCII.GetString(bytes, offset, length)
            Else
                With New StringBuilder
                    If bytes IsNot Nothing Then
                        Dim asciiString As String = Encoding.ASCII.GetString(bytes, offset, length)

                        For x As Integer = 0 To asciiString.Length - 1
                            If x > 0 Then .Append(spacingCharacter)
                            .Append(asciiString(x))
                        Next
                    End If

                    Return .ToString()
                End With
            End If

        End Function

    End Class

#End Region

    Public Const NoSpacing As Char = Char.MinValue

    Private Shared m_hexadecimalEncoding As HexadecimalEncoding
    Private Shared m_decimalEncoding As DecimalEncoding
    Private Shared m_bigEndianBinaryEncoding As BinaryEncoding
    Private Shared m_littleEndianBinaryEncoding As BinaryEncoding
    Private Shared m_base64Encoding As Base64Encoding
    Private Shared m_asciiEncoding As ASCIIEncoding

    Private Sub New()

        ' This class contains only global functions and is not meant to be instantiated.

    End Sub

    ''' <summary>Handles encoding and decoding of a byte buffer into a hexadecimal-based presentation format.</summary>
    Public Shared ReadOnly Property Hexadecimal() As HexadecimalEncoding
        Get
            If m_hexadecimalEncoding Is Nothing Then m_hexadecimalEncoding = New HexadecimalEncoding
            Return m_hexadecimalEncoding
        End Get
    End Property

    ''' <summary>Handles encoding and decoding of a byte buffer into an integer-based presentation format.</summary>
    Public Shared ReadOnly Property [Decimal]() As DecimalEncoding
        Get
            If m_decimalEncoding Is Nothing Then m_decimalEncoding = New DecimalEncoding
            Return m_decimalEncoding
        End Get
    End Property

    ''' <summary>Handles encoding and decoding of a byte buffer into a big-endian binary (i.e., 0 and 1's) based
    ''' presentation format.</summary>
    ''' <remarks>
    ''' Although endianness is typically used in the context of byte order (see TVA.Interop.EndianOrder to handle byte 
    ''' order swapping), this property allows you visualize "bits" in big-endian order, right-to-left (Note that bits
    ''' are normally stored in the same order as their bytes.).
    ''' </remarks>
    Public Shared ReadOnly Property BigEndianBinary() As BinaryEncoding
        Get
            If m_bigEndianBinaryEncoding Is Nothing Then m_bigEndianBinaryEncoding = New BinaryEncoding(Endianness.BigEndian)
            Return m_bigEndianBinaryEncoding
        End Get
    End Property

    ''' <summary>Handles encoding and decoding of a byte buffer into a little-endian binary (i.e., 0 and 1's) based
    ''' presentation format.</summary>
    ''' <remarks>
    ''' Although endianness is typically used in the context of byte order (see TVA.Interop.EndianOrder to handle byte
    ''' order swapping), this property allows you visualize "bits" in little-endian order, left-to-right (Note that bits
    ''' are normally stored in the same order as their bytes.).
    ''' </remarks>
    Public Shared ReadOnly Property LittleEndianBinary() As BinaryEncoding
        Get
            If m_littleEndianBinaryEncoding Is Nothing Then m_littleEndianBinaryEncoding = New BinaryEncoding(Endianness.LittleEndian)
            Return m_littleEndianBinaryEncoding
        End Get
    End Property

    ''' <summary>Handles encoding and decoding of a byte buffer into a base64 presentation format.</summary>
    Public Shared ReadOnly Property Base64() As Base64Encoding
        Get
            If m_base64Encoding Is Nothing Then m_base64Encoding = New Base64Encoding
            Return m_base64Encoding
        End Get
    End Property

    ''' <summary>Handles encoding and decoding of a byte buffer into an ASCII character presentation format.</summary>
    Public Shared ReadOnly Property ASCII() As ASCIIEncoding
        Get
            If m_asciiEncoding Is Nothing Then m_asciiEncoding = New ASCIIEncoding
            Return m_asciiEncoding
        End Get
    End Property

#Region " Inheritable Functionality "

    ''' <summary>Encodes given buffer into a user presentable representation.</summary>
    ''' <param name="bytes">Bytes to encode.</param>

    Public Overridable Function GetString(ByVal bytes As Byte()) As String

        Return GetString(bytes, NoSpacing)

    End Function

    ''' <summary>Encodes given buffer into a user presentable representation.</summary>
    ''' <param name="bytes">Bytes to encode.</param>
    ''' <param name="spacingCharacter">Spacing character to place between encoded bytes.</param>
    ''' <returns>String of encoded bytes.</returns>
    Public Overridable Function GetString(ByVal bytes As Byte(), ByVal spacingCharacter As Char) As String

        If bytes Is Nothing Then Throw New ArgumentNullException("bytes", "Input buffer cannot be null")
        Return GetString(bytes, 0, bytes.Length, spacingCharacter)

    End Function

    ''' <summary>Encodes given buffer into a user presentable representation.</summary>
    ''' <param name="bytes">Bytes to encode.</param>
    ''' <param name="offset">Offset into buffer to begin encoding.</param>
    ''' <param name="length">Length of buffer to encode.</param>
    ''' <returns>String of encoded bytes.</returns>
    Public Overridable Function GetString(ByVal bytes As Byte(), ByVal offset As Integer, ByVal length As Integer) As String

        If bytes Is Nothing Then Throw New ArgumentNullException("bytes", "Input buffer cannot be null")
        Return GetString(bytes, offset, length, NoSpacing)

    End Function

    ''' <summary>Encodes given buffer into a user presentable representation.</summary>
    ''' <param name="bytes">Bytes to encode.</param>
    ''' <param name="offset">Offset into buffer to begin encoding.</param>
    ''' <param name="length">Length of buffer to encode.</param>
    ''' <param name="spacingCharacter">Spacing character to place between encoded bytes.</param>
    ''' <returns>String of encoded bytes.</returns>
    Public MustOverride Function GetString(ByVal bytes As Byte(), ByVal offset As Integer, ByVal length As Integer, ByVal spacingCharacter As Char) As String

    ''' <summary>Decodes given string back into a byte buffer.</summary>
    ''' <param name="value">Encoded string to decode.</param>
    ''' <returns>Decoded bytes.</returns>
    Public Overridable Function GetBytes(ByVal value As String) As Byte()

        If String.IsNullOrEmpty(value) Then Throw New ArgumentNullException("value", "Input string cannot be null")
        Return GetBytes(value, NoSpacing)

    End Function

    ''' <summary>Decodes given string back into a byte buffer.</summary>
    ''' <param name="value">Encoded string to decode.</param>
    ''' <param name="spacingCharacter">Original spacing character that was inserted between encoded bytes.</param>
    ''' <returns>Decoded bytes</returns>
    Public MustOverride Function GetBytes(ByVal value As String, ByVal spacingCharacter As Char) As Byte()

    ''' <summary>Handles byte to string conversions for implementations that are available from Byte.ToString.</summary>
    Protected Function BytesToString(ByVal bytes As Byte(), ByVal offset As Integer, ByVal length As Integer, ByVal spacingCharacter As Char, ByVal format As String) As String

        If bytes Is Nothing Then Throw New ArgumentNullException("bytes", "Input buffer cannot be null")

        With New StringBuilder
            If bytes IsNot Nothing Then
                For x As Integer = 0 To length - 1
                    If spacingCharacter <> NoSpacing AndAlso x > 0 Then .Append(spacingCharacter)
                    .Append(bytes(x + offset).ToString(format))
                Next
            End If

            Return .ToString()
        End With

    End Function

#End Region

End Class