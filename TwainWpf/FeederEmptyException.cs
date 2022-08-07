using System;
using System.Runtime.Serialization;

namespace TwainWpf
{
    public class FeederEmptyException : TwainException
    {
        public FeederEmptyException()
            : this(null, null)
        {
        }

        public FeederEmptyException(string message)
            : this(message, null)
        {
        }

        public FeederEmptyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected FeederEmptyException(SerializationInfo info, StreamingContext context) :
                    base(info, context)
        {
        }

        public FeederEmptyException(string message, TwainNative.TwainResult returnCode) : base(message, returnCode)
        {
        }

        public FeederEmptyException(string message, TwainNative.TwainResult returnCode, TwainNative.ConditionCode conditionCode) : base(message, returnCode, conditionCode)
        {
        }
    }
}