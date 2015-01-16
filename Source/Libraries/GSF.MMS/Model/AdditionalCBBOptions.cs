//
// This file was generated by the BinaryNotes compiler.
// See http://bnotes.sourceforge.net 
// Any modifications to this file will be lost upon recompilation of the source ASN.1. 
//

using System.Runtime.CompilerServices;
using GSF.ASN1;
using GSF.ASN1.Attributes;
using GSF.ASN1.Attributes.Constraints;
using GSF.ASN1.Coders;
using GSF.ASN1.Types;

namespace GSF.MMS.Model
{
    
    [ASN1PreparedElement]
    [ASN1BoxedType(Name = "AdditionalCBBOptions")]
    public class AdditionalCBBOptions : IASN1PreparedElement
    {
        private static readonly IASN1PreparedElementData preparedData = CoderFactory.getInstance().newPreparedElementData(typeof(AdditionalCBBOptions));
        private BitString val;

        public AdditionalCBBOptions()
        {
        }

        public AdditionalCBBOptions(BitString value)
        {
            Value = value;
        }

        [ASN1BitString(Name = "AdditionalCBBOptions")]
        [ASN1SizeConstraint(Max = 3L)]
        public BitString Value
        {
            get
            {
                return val;
            }
            set
            {
                val = value;
            }
        }


        public void initWithDefaults()
        {
        }

        public IASN1PreparedElementData PreparedData
        {
            get
            {
                return preparedData;
            }
        }
    }
}