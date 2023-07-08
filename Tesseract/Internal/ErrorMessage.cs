namespace Tesseract.Internal
{
    internal static class ErrorMessage
    {
        private const string ErrorMessageFormat = "{0}. See {1} for details.";
        private const string WikiUrlFormat = "https://github.com/charlesw/tesseract/wiki/Error-{0}";

        public static string ErrorPageUrl(int errorNumber) { return string.Format(WikiUrlFormat, errorNumber); }

        public static string Format(int errorNumber, string messageFormat, params object[] messageArgs)
        {
            string errorMessage = string.Format(messageFormat, messageArgs);
            string errorPageUrl = ErrorPageUrl(errorNumber);
            return string.Format(ErrorMessageFormat, errorMessage, errorPageUrl);
        }
    }
}