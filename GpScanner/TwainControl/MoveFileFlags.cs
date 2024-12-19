using System;

namespace TwainControl;

public partial class TwainCtrl
{
    [Flags]
    private enum MoveFileFlags : uint
    {
        MOVEFILE_REPLACE_EXISTING = 0x1,
        MOVEFILE_DELAY_UNTIL_REBOOT = 0x4
    }
}