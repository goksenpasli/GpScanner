using TwainControl;

namespace GpScanner
{
    public class TwainService(TwainCtrl twainCtrl) : ITwainService
    {
        public TwainCtrl TwainCtrl => twainCtrl;
    }
}