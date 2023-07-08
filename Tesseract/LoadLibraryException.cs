using System;
using System.Runtime.Serialization;

namespace Tesseract
{
    [Serializable]
    public class LoadLibraryException : SystemException
    {
        protected LoadLibraryException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
        public LoadLibraryException()
        {
        }

        public LoadLibraryException(string message) : base(message)
        {
        }

        public LoadLibraryException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}