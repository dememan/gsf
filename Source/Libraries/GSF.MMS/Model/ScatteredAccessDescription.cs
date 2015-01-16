//
// This file was generated by the BinaryNotes compiler.
// See http://bnotes.sourceforge.net 
// Any modifications to this file will be lost upon recompilation of the source ASN.1. 
//

using System.Runtime.CompilerServices;
using System.Collections.Generic;
using GSF.ASN1;
using GSF.ASN1.Attributes;
using GSF.ASN1.Coders;

namespace GSF.MMS.Model
{
    
    [ASN1PreparedElement]
    [ASN1BoxedType(Name = "ScatteredAccessDescription")]
    public class ScatteredAccessDescription : IASN1PreparedElement
    {
        private static readonly IASN1PreparedElementData preparedData = CoderFactory.getInstance().newPreparedElementData(typeof(ScatteredAccessDescription));
        private ICollection<ScatteredAccessDescriptionSequenceType> val;


        [ASN1SequenceOf(Name = "ScatteredAccessDescription", IsSetOf = false)]
        public ICollection<ScatteredAccessDescriptionSequenceType> Value
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

        public void initValue()
        {
            Value = new List<ScatteredAccessDescriptionSequenceType>();
        }

        public void Add(ScatteredAccessDescriptionSequenceType item)
        {
            Value.Add(item);
        }

        [ASN1PreparedElement]
        [ASN1Sequence(Name = "ScatteredAccessDescription", IsSet = false)]
        public class ScatteredAccessDescriptionSequenceType : IASN1PreparedElement
        {
            private static IASN1PreparedElementData preparedData = CoderFactory.getInstance().newPreparedElementData(typeof(ScatteredAccessDescriptionSequenceType));
            private AlternateAccess alternateAccess_;

            private bool alternateAccess_present;
            private Identifier componentName_;

            private bool componentName_present;


            private VariableSpecification variableSpecification_;

            [ASN1Element(Name = "componentName", IsOptional = true, HasTag = true, Tag = 0, HasDefaultValue = false)]
            public Identifier ComponentName
            {
                get
                {
                    return componentName_;
                }
                set
                {
                    componentName_ = value;
                    componentName_present = true;
                }
            }

            [ASN1Element(Name = "variableSpecification", IsOptional = false, HasTag = true, Tag = 1, HasDefaultValue = false)]
            public VariableSpecification VariableSpecification
            {
                get
                {
                    return variableSpecification_;
                }
                set
                {
                    variableSpecification_ = value;
                }
            }


            [ASN1Element(Name = "alternateAccess", IsOptional = true, HasTag = true, Tag = 2, HasDefaultValue = false)]
            public AlternateAccess AlternateAccess
            {
                get
                {
                    return alternateAccess_;
                }
                set
                {
                    alternateAccess_ = value;
                    alternateAccess_present = true;
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

            public bool isComponentNamePresent()
            {
                return componentName_present;
            }

            public bool isAlternateAccessPresent()
            {
                return alternateAccess_present;
            }
        }
    }
}