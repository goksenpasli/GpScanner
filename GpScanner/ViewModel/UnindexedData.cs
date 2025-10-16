using Extensions;

namespace GpScanner
{
    public class UnindexedData : InpcBase
    {
        public string FileName
        {
            get;
            set => SetProperty(ref field, value);
        }
        public bool HasError
        {
            get;
            set => SetProperty(ref field, value);
        }
        public string Error
        {
            get;
            set => SetProperty(ref field, value);
        }
    }
}