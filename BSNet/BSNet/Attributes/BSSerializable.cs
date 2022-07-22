using System;
using System.Text;
using BSNet.Quantization;

namespace BSNet.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class BSSerializableAttribute : Attribute
    {
        private BoundedRange range;
        private Encoding encoding = Encoding.ASCII;
        
        public BSSerializableAttribute() {}

        public BSSerializableAttribute(Type encodingType)
        {
            this.encoding = (Encoding)Activator.CreateInstance(encodingType);
        }

        public BSSerializableAttribute(float min, float max, float precision)
        {
            range = new BoundedRange(min, max, precision);
        }
    }
}