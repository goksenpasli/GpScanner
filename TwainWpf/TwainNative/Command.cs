namespace TwainWpf.TwainNative
{
    public enum Command
    {
        Not = -1,

        Null = 0,

        TransferReady = 1,

        CloseRequest = 2,

        CloseOk = 3,

        DeviceEvent = 4
    }
}