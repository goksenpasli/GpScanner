using System;
using System.Runtime.Serialization;
using TwainWpf.TwainNative;

namespace TwainWpf
{
    public class FeederEmptyException : TwainException
    {
        public FeederEmptyException() : this(null, null)
        {
        }

        public FeederEmptyException(string message) : this(message, null)
        {
        }

        public FeederEmptyException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public FeederEmptyException(string message, TwainResult returnCode) : base(message, returnCode)
        {
        }

        public FeederEmptyException(string message, TwainResult returnCode, ConditionCode conditionCode) : base(message, returnCode, conditionCode)
        {
        }

        protected FeederEmptyException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}