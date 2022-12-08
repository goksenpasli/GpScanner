using System;

namespace Tesseract.Internal
{
    internal static class ErrorMessage
    {
        public static string ErrorPageUrl(int errorNumber)
        {
            return String.Format(WikiUrlFormat, errorNumber);
        }

        public static string Format(int errorNumber, string messageFormat, params object[] messageArgs)
        {
            var errorMessage = String.Format(messageFormat, messageArgs);
            var errorPageUrl = ErrorPageUrl(errorNumber);
            return String.Format(ErrorMessageFormat, errorMessage, errorPageUrl);
        }

        private const string ErrorMessageFormat = "{0}. See {1} for details.";

        private const string WikiUrlFormat = "https://github.com/charlesw/tesseract/wiki/Error-{0}";
    }
}