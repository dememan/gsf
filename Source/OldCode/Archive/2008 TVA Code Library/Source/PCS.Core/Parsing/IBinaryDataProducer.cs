//*******************************************************************************************************
//  IBinaryDataProducer.cs
//  Copyright © 2008 - TVA, all rights reserved - Gbtc
//
//  Build Environment: C#, Visual Studio 2008
//  Primary Developer: James R Carroll
//      Office: PSO TRAN & REL, CHATTANOOGA - MR 2W-C
//       Phone: 423/751-2827
//       Email: jrcarrol@tva.gov
//
//  Code Modification History:
//  -----------------------------------------------------------------------------------------------------
//  03/01/2007 - Pinal C. Patel
//       Original version of source code generated.
//  09/10/2008 - J. Ritchie Carroll
//      Converted to C#.
//  10/28/2008 - Pinal C. Patel
//      Edited code comments.
//
//*******************************************************************************************************

namespace PCS.Parsing
{
    /// <summary>
    /// Specifies that this <see cref="System.Type"/> can provide a binary image of the object.
    /// </summary>
    public interface IBinaryDataProducer
    {
        /// <summary>
        /// Gets the binary image of the object.
        /// </summary>
        byte[] BinaryImage
        {
            get;
        }

        /// <summary>
        /// Gets the length of the binary image.
        /// </summary>
        /// <remarks>
        /// <see cref="BinaryLength"/> should typically be a constant value but does not have to be.
        /// </remarks>
        int BinaryLength
        {
            get;
        }
    }
}