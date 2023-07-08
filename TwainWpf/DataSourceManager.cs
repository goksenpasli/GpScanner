using System;
using System.Reflection;
using System.Runtime.InteropServices;
using TwainWpf.TwainNative;
using TwainWpf.Win32;

namespace TwainWpf
{
    /// <summary>
    /// DataSourceManager
    /// </summary>
    /// <seealso cref="IDisposable"/>
    public class DataSourceManager : IDisposable
    {
        public static readonly Identity DefaultApplicationId = new Identity()
        {
            Id = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0),
            Version = new TwainVersion() { MajorNum = 1, MinorNum = 1, Language = Language.USA, Country = Country.USA, Info = Assembly.GetExecutingAssembly().FullName },
            ProtocolMajor = TwainConstants.ProtocolMajor,
            ProtocolMinor = TwainConstants.ProtocolMinor,
            SupportedGroups = (int)(DataGroup.Image | DataGroup.Control),
            Manufacturer = "TwainDotNet",
            ProductFamily = "TwainDotNet",
            ProductName = "TwainDotNet",
        };
        private Event _eventMessage;

        public DataSourceManager(Identity applicationId, IWindowsMessageHook messageHook)
        {
            ApplicationId = applicationId.Clone();

            ScanningComplete += delegate
            {
            };
            TransferImage += delegate
            {
            };

            MessageHook = messageHook;
            MessageHook.FilterMessageCallback = FilterMessage;
            IntPtr windowHandle = MessageHook.WindowHandle;

            _eventMessage.EventPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WindowsMessage)));

            TwainResult result = Twain32Native.DsmParent(ApplicationId, IntPtr.Zero, DataGroup.Control, DataArgumentType.Parent, Message.OpenDSM, ref windowHandle);

            DataSource = result == TwainResult.Success ? DataSource.GetDefault(ApplicationId, MessageHook) : throw new TwainException($"Error initialising DSM: {result}", result);
        }

        ~DataSourceManager() { Dispose(false); }

        /// <summary>
        /// Notification that the scanning has completed.
        /// </summary>
        public event EventHandler<ScanningCompleteEventArgs> ScanningComplete;

        public event EventHandler<TransferImageEventArgs> TransferImage;

        public Identity ApplicationId { get; }

        public DataSource DataSource { get; private set; }

        public IWindowsMessageHook MessageHook { get; }

        public static ConditionCode GetConditionCode(Identity applicationId, Identity sourceId)
        {
            Status status = new Status();

            _ = Twain32Native.DsmStatus(applicationId, sourceId, DataGroup.Control, DataArgumentType.Status, Message.Get, status);

            return status.ConditionCode;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void SelectSource()
        {
            DataSource.Dispose();
            DataSource = DataSource.UserSelected(ApplicationId, MessageHook);
        }

        public void SelectSource(DataSource dataSource)
        {
            DataSource.Dispose();
            DataSource = dataSource;
        }

        public void StartScan(ScanSettings settings)
        {
            bool scanning = false;

            try
            {
                MessageHook.UseFilter = true;
                scanning = DataSource.Open(settings);
            } catch(TwainException)
            {
                DataSource.Close();
                EndingScan();
                throw;
            } finally
            {
                if(!scanning)
                {
                    EndingScan();
                }
            }
        }

        internal void CloseDsAndCompleteScanning(Exception exception)
        {
            EndingScan();
            DataSource.Close();
            try
            {
                ScanningComplete?.Invoke(this, new ScanningCompleteEventArgs(exception));
            } catch
            {
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            Marshal.FreeHGlobal(_eventMessage.EventPtr);

            if(disposing)
            {
                DataSource.Dispose();

                IntPtr windowHandle = MessageHook.WindowHandle;

                if(ApplicationId.Id != 0)
                {
                    _ = Twain32Native.DsmParent(ApplicationId, IntPtr.Zero, DataGroup.Control, DataArgumentType.Parent, Message.CloseDSM, ref windowHandle);
                }

                ApplicationId.Id = 0;
            }
        }

        protected void EndingScan() { MessageHook.UseFilter = false; }

        protected IntPtr FilterMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if(DataSource.SourceId.Id == 0)
            {
                handled = false;
                return IntPtr.Zero;
            }

            int pos = User32Native.GetMessagePos();

            WindowsMessage message = new WindowsMessage { hwnd = hwnd, message = msg, wParam = wParam, lParam = lParam, time = User32Native.GetMessageTime(), x = (short)pos, y = (short)(pos >> 16) };

            Marshal.StructureToPtr(message, _eventMessage.EventPtr, false);
            _eventMessage.Message = 0;

            TwainResult result = Twain32Native.DsEvent(ApplicationId, DataSource.SourceId, DataGroup.Control, DataArgumentType.Event, Message.ProcessEvent, ref _eventMessage);

            if(result == TwainResult.NotDSEvent)
            {
                handled = false;
                return IntPtr.Zero;
            }

            switch(_eventMessage.Message)
            {
                case Message.XFerReady:
                    Exception exception = null;
                    try
                    {
                        TransferPictures();
                    } catch(Exception e)
                    {
                        exception = e;
                    }
                    CloseDsAndCompleteScanning(exception);
                    break;

                case Message.CloseDS:
                case Message.CloseDSOK:
                case Message.CloseDSReq:
                    CloseDsAndCompleteScanning(null);
                    break;

                case Message.DeviceEvent:
                    break;
            }

            handled = true;
            return IntPtr.Zero;
        }

        protected void TransferPictures()
        {
            if(DataSource.SourceId.Id == 0)
            {
                return;
            }

            PendingXfers pendingTransfer = new PendingXfers();
            try
            {
                do
                {
                    pendingTransfer.Count = 0;
                    IntPtr hbitmap = IntPtr.Zero;

                    ImageInfo imageInfo = new ImageInfo();
                    TwainResult result = Twain32Native.DsImageInfo(ApplicationId, DataSource.SourceId, DataGroup.Image, DataArgumentType.ImageInfo, Message.Get, imageInfo);

                    if(result != TwainResult.Success)
                    {
                        DataSource.Close();
                        break;
                    }

                    result = Twain32Native.DsImageTransfer(ApplicationId, DataSource.SourceId, DataGroup.Image, DataArgumentType.ImageNativeXfer, Message.Get, ref hbitmap);

                    if(result != TwainResult.XferDone)
                    {
                        DataSource.Close();
                        break;
                    }

                    result = Twain32Native.DsPendingTransfer(ApplicationId, DataSource.SourceId, DataGroup.Control, DataArgumentType.PendingXfers, Message.EndXfer, pendingTransfer);

                    if(result != TwainResult.Success)
                    {
                        DataSource.Close();
                        break;
                    }

                    if(hbitmap != IntPtr.Zero)
                    {
                        using(BitmapRenderer renderer = new BitmapRenderer(hbitmap))
                        {
                            TransferImageEventArgs args = new TransferImageEventArgs(renderer.RenderToBitmap(), pendingTransfer.Count != 0);
                            TransferImage?.Invoke(this, args);
                            if(!args.ContinueScanning)
                            {
                                break;
                            }
                        }
                    }
                } while (pendingTransfer.Count != 0);
            } finally
            {
                _ = Twain32Native.DsPendingTransfer(ApplicationId, DataSource.SourceId, DataGroup.Control, DataArgumentType.PendingXfers, Message.Reset, pendingTransfer);
            }
        }
    }
}