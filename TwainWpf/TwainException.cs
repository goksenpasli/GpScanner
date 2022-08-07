using System;
using System.Runtime.Serialization;
using TwainWpf.TwainNative;

namespace TwainWpf
{
    public class TwainException : ApplicationException
    {
        public TwainException()
            : this(null, null)
        {
        }

        public TwainException(string message)
            : this(message, null)
        {
        }

        public TwainException(string message, TwainResult returnCode)
            : this(message, null)
        {
            ReturnCode = returnCode;
        }

        public TwainException(string message, TwainResult returnCode, ConditionCode conditionCode)
            : this(message, null)
        {
            ReturnCode = returnCode;
            ConditionCode = conditionCode;
        }

        public TwainException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public ConditionCode? ConditionCode { get; private set; }

        public TwainResult? ReturnCode { get; private set; }

        protected TwainException(SerializationInfo info, StreamingContext context) :
                                    base(info, context)
        {
        }
    }
}