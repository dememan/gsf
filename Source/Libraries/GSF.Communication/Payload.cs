//******************************************************************************************************
//  Payload.cs - Gbtc
//
//  Copyright � 2012, Grid Protection Alliance.  All Rights Reserved.
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
//  07/06/2006 - Pinal C. Patel
//       Original version of source code generated.
//  09/29/2008 - J. Ritchie Carroll
//       Converted to C#.
//  09/14/2009 - Stephen C. Wills
//       Added new header and license agreement.
//  09/21/2009 - Pinal C. Patel
//       Fixed a bug in AddHeader() that was putting the length of the provided buffer in the header 
//       instead of the length specified in the method parameter.
//  01/30/2011 - Pinal C. Patel
//       Fixed a bug in AddHeader() that created an insufficient return buffer when the specified offset 
//       was non-zero resulting in "out of bounds" exception.
//  08/18/2011 - J. Ritchie Carroll
//       Added processing overloads to be able to use socket asynchronous event arguments and performed
//       minor code clean up and code review.
//  12/13/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System;

namespace GSF.Communication
{
    /// <summary>
    /// A helper class containing methods for manipulation of payload.
    /// </summary>
    public static class Payload
    {
        /// <summary>
        /// Specifies the length of the segment in a "Payload-Aware" transmission that contains the payload length.
        /// </summary>
        public const int LengthSegment = 4;

        /// <summary>
        /// Default byte sequence used to mark the beginning of the payload in a "Payload-Aware" transmissions.
        /// </summary>
        public static byte[] DefaultMarker = { 0xAA, 0xBB, 0xCC, 0xDD };

        /// <summary>
        /// Adds header containing the <paramref name="marker"/> to the payload in the <paramref name="buffer"/> for "Payload-Aware" transmission.
        /// </summary>
        /// <param name="buffer">The buffer containing the payload.</param>
        /// <param name="offset">The offset in the <paramref name="buffer"/> at which the payload starts.</param>
        /// <param name="length">The lenght of the payload in the <paramref name="buffer"/> starting at the <paramref name="offset"/>.</param>
        /// <param name="marker">The byte sequence used to mark the beginning of the payload in a "Payload-Aware" transmissions.</param>
        public static void AddHeader(ref byte[] buffer, ref int offset, ref int length, byte[] marker)
        {
            // Note that the resulting buffer will be at least 4 bytes bigger than the payload

            // Resulting buffer = x bytes for payload marker + 4 bytes for the payload size + The payload
            byte[] result = new byte[length + marker.Length + LengthSegment];

            // First, copy the the payload marker to the buffer
            Buffer.BlockCopy(marker, 0, result, 0, marker.Length);

            // Then, copy the payload's size to the buffer after the payload marker
            Buffer.BlockCopy(LittleEndian.GetBytes(length), 0, result, marker.Length, LengthSegment);

            // At last, copy the payload after the payload marker and payload size
            Buffer.BlockCopy(buffer, offset, result, marker.Length + LengthSegment, length);

            buffer = result;
            offset = 0;
            length = buffer.Length;
        }

        /// <summary>
        /// Determines whether or not the <paramref name="buffer"/> contains the header information of a "Payload-Aware" transmission.
        /// </summary>
        /// <param name="buffer">The buffer to be checked at index zero.</param>
        /// <param name="marker">The byte sequence used to mark the beginning of the payload in a "Payload-Aware" transmissions.</param>
        /// <returns>true if the buffer contains "Payload-Aware" transmission header; otherwise false.</returns>
        public static bool HasHeader(byte[] buffer, byte[] marker)
        {
            for (int i = 0; i < marker.Length; i++)
            {
                if (buffer[i] != marker[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines the length of a payload in a "Payload-Aware" transmission from the payload header information.
        /// </summary>
        /// <param name="buffer">The buffer containg payload header information starting at index zero.</param>
        /// <param name="length">The length of valid data within in <paramref name="buffer"/>.</param>
        /// <param name="marker">The byte sequence used to mark the beginning of the payload in a "Payload-Aware" transmissions.</param>
        /// <returns>Length of the payload.</returns>
        public static int ExtractLength(byte[] buffer, int length, byte[] marker)
        {
            // Check to see if buffer is at least as big as the payload header and has the payload marker
            if (length >= (marker.Length + LengthSegment) && HasHeader(buffer, marker))
                return BitConverter.ToInt32(buffer, marker.Length);

            return -1;
        }
    }
}