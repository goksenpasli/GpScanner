using System;
using System.Collections.Generic;
using TwainWpf.TwainNative;

namespace TwainWpf {
    public class DataSource : IDisposable {
        public DataSource(Identity applicationId, Identity sourceId, IWindowsMessageHook messageHook) {
            _applicationId = applicationId;
            SourceId = sourceId.Clone();
            _messageHook = messageHook;
        }

        public bool PaperDetectable {
            get {
                try {
                    return Capability.GetBoolCapability(Capabilities.FeederLoaded, _applicationId, SourceId);
                }
                catch {
                    return false;
                }
            }
        }

        public Identity SourceId { get; }

        public bool SupportsDuplex {
            get {
                try {
                    Capability cap = new Capability(Capabilities.Duplex, TwainType.Int16, _applicationId, SourceId);
                    return ((Duplex)cap.GetBasicValue().Int16Value) != Duplex.None;
                }
                catch {
                    return false;
                }
            }
        }

        public bool SupportsFilmScanner {
            get {
                try {
                    Capability cap = new Capability(Capabilities.Lightpath, TwainType.Int16, _applicationId, SourceId);

                    //return ((Lightpath)cap.GetBasicValue().Int16Value) != Lightpath.Transmissive;
                    return true;
                }
                catch {
                    return false;
                }
            }
        }

        public static List<DataSource> GetAllSources(Identity applicationId, IWindowsMessageHook messageHook) {
            List<DataSource> sources = new List<DataSource>();
            Identity id = new Identity();

            // Get the first source
            TwainResult result = Twain32Native.DsmIdentity(
                applicationId,
                IntPtr.Zero,
                DataGroup.Control,
                DataArgumentType.Identity,
                Message.GetFirst,
                id);

            if (result == TwainResult.EndOfList) {
                return sources;
            }
            else if (result != TwainResult.Success) {
                throw new TwainException("Error getting first source.", result);
            }
            else {
                sources.Add(new DataSource(applicationId, id, messageHook));
            }

            while (true) {
                // Get the next source
                result = Twain32Native.DsmIdentity(
                    applicationId,
                    IntPtr.Zero,
                    DataGroup.Control,
                    DataArgumentType.Identity,
                    Message.GetNext,
                    id);

                if (result == TwainResult.EndOfList) {
                    break;
                }
                else if (result != TwainResult.Success) {
                    throw new TwainException("Error enumerating sources.", result);
                }

                sources.Add(new DataSource(applicationId, id, messageHook));
            }

            return sources;
        }

        public static DataSource GetDefault(Identity applicationId, IWindowsMessageHook messageHook) {
            Identity defaultSourceId = new Identity();

            // Attempt to get information about the system default source
            TwainResult result = Twain32Native.DsmIdentity(
                applicationId,
                IntPtr.Zero,
                DataGroup.Control,
                DataArgumentType.Identity,
                Message.GetDefault,
                defaultSourceId);

            if (result != TwainResult.Success) {
                ConditionCode status = DataSourceManager.GetConditionCode(applicationId, null);
                throw new TwainException("Error getting information about the default source: " + result, result, status);
            }

            return new DataSource(applicationId, defaultSourceId, messageHook);
        }

        public static DataSource GetSource(string sourceProductName, Identity applicationId, IWindowsMessageHook messageHook) {
            // A little slower than it could be, if enumerating unnecessary sources is slow. But less code duplication.
            foreach (DataSource source in GetAllSources(applicationId, messageHook)) {
                if (sourceProductName.Equals(source.SourceId.ProductName, StringComparison.InvariantCultureIgnoreCase)) {
                    return source;
                }
            }

            return null;
        }

        public static DataSource UserSelected(Identity applicationId, IWindowsMessageHook messageHook) {
            Identity defaultSourceId = new Identity();

            // Show the TWAIN interface to allow the user to select a source
            _ = Twain32Native.DsmIdentity(
                applicationId,
                IntPtr.Zero,
                DataGroup.Control,
                DataArgumentType.Identity,
                Message.UserSelect,
                defaultSourceId);

            return new DataSource(applicationId, defaultSourceId, messageHook);
        }

        public void Close() {
            if (SourceId.Id != 0) {
                try {
                    UserInterface userInterface = new UserInterface();

                    TwainResult result = Twain32Native.DsUserInterface(
                        _applicationId,
                        SourceId,
                        DataGroup.Control,
                        DataArgumentType.UserInterface,
                        Message.DisableDS,
                        userInterface);

                    if (result != TwainResult.Failure) {
                        result = Twain32Native.DsmIdentity(
                            _applicationId,
                            IntPtr.Zero,
                            DataGroup.Control,
                            DataArgumentType.Identity,
                            Message.CloseDS,
                            SourceId);
                    }
                }
                catch {
                    // ignore this is bypass an error that if trigerd needs the whole twain classto be reinitialised
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool Enable(ScanSettings settings) {
            UserInterface ui = new UserInterface {
                ShowUI = (short)(settings.ShowTwainUi ? 1 : 0),
                ModalUI = 1,
                ParentHand = _messageHook.WindowHandle
            };

            TwainResult result = Twain32Native.DsUserInterface(
                _applicationId,
                SourceId,
                DataGroup.Control,
                DataArgumentType.UserInterface,
                Message.EnableDS,
                ui);

            if (result != TwainResult.Success) {
                Dispose();
                return false;
            }
            return true;
        }

        public short GetBitDepth(ScanSettings scanSettings) {
            switch (scanSettings.Resolution.ColourSetting) {
                case ColourSetting.BlackAndWhite:
                    return 1;

                case ColourSetting.GreyScale:
                    return 8;

                case ColourSetting.Colour:
                    return 16;
            }

            throw new NotImplementedException();
        }

        public PixelType GetPixelType(ScanSettings scanSettings) {
            switch (scanSettings.Resolution.ColourSetting) {
                case ColourSetting.BlackAndWhite:
                    return PixelType.BlackAndWhite;

                case ColourSetting.GreyScale:
                    return PixelType.Grey;

                case ColourSetting.Colour:
                    return PixelType.Rgb;
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Negotiates the automatic border detection capability.
        /// </summary>
        /// <param name="scanSettings">The scan settings.</param>
        public void NegotiateAutomaticBorderDetection(ScanSettings scanSettings) {
            try {
                if (scanSettings.Rotation.AutomaticBorderDetection) {
                    Capability.SetCapability(Capabilities.Automaticborderdetection, true, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        /// <summary>
        /// Negotiates the automatic rotation capability.
        /// </summary>
        /// <param name="scanSettings">The scan settings.</param>
        public void NegotiateAutomaticRotate(ScanSettings scanSettings) {
            try {
                if (scanSettings.Rotation.AutomaticRotate) {
                    Capability.SetCapability(Capabilities.Automaticrotate, true, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        public void NegotiateColour(ScanSettings scanSettings) {
            try {
                _ = Capability.SetBasicCapability(Capabilities.IPixelType, (ushort)GetPixelType(scanSettings), TwainType.UInt16, _applicationId, SourceId);
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }

            // TODO: Also set this for colour scanning
            try {
                if (scanSettings.Resolution.ColourSetting != ColourSetting.Colour) {
                    _ = Capability.SetCapability(Capabilities.BitDepth, GetBitDepth(scanSettings), _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        public void NegotiateDuplex(ScanSettings scanSettings) {
            try {
                if (scanSettings.UseDuplex.HasValue && SupportsDuplex) {
                    Capability.SetCapability(Capabilities.DuplexEnabled, scanSettings.UseDuplex.Value, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        public void NegotiateFeeder(ScanSettings scanSettings) {
            try {
                if (scanSettings.UseDocumentFeeder.HasValue) {
                    Capability.SetCapability(Capabilities.FeederEnabled, scanSettings.UseDocumentFeeder.Value, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }

            try {
                if (scanSettings.UseAutoFeeder.HasValue) {
                    Capability.SetCapability(Capabilities.AutoFeed, scanSettings.UseAutoFeeder == true && scanSettings.UseDocumentFeeder == true, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }

            try {
                if (scanSettings.UseAutoScanCache.HasValue) {
                    Capability.SetCapability(Capabilities.AutoScan, scanSettings.UseAutoScanCache.Value, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        public void NegotiateLightPath(ScanSettings scanSettings) {
            try {
                if (scanSettings.UseFilmScanner.HasValue && SupportsFilmScanner) {
                    _ = scanSettings.UseFilmScanner.Value ? Capability.SetBasicCapability(Capabilities.Lightpath, (ushort)Lightpath.Transmissive, TwainType.UInt16, _applicationId, SourceId)
                        : Capability.SetBasicCapability(Capabilities.Lightpath, (ushort)Lightpath.Reflective, TwainType.UInt16, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        public void NegotiateOrientation(ScanSettings scanSettings) {
            // Set orientation (default is portrait)
            try {
                Capability cap = new Capability(Capabilities.Orientation, TwainType.Int16, _applicationId, SourceId);
                if ((Orientation)cap.GetBasicValue().Int16Value != Orientation.Default) {
                    _ = Capability.SetBasicCapability(Capabilities.Orientation, (ushort)scanSettings.Page.Orientation, TwainType.UInt16, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        /// <summary>
        /// Negotiates the size of the page.
        /// </summary>
        /// <param name="scanSettings">The scan settings.</param>
        public void NegotiatePageSize(ScanSettings scanSettings) {
            try {
                Capability cap = new Capability(Capabilities.Supportedsizes, TwainType.Int16, _applicationId, SourceId);
                if ((PageType)cap.GetBasicValue().Int16Value != PageType.UsLetter) {
                    _ = Capability.SetBasicCapability(Capabilities.Supportedsizes, (ushort)scanSettings.Page.Size, TwainType.UInt16, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        /// <summary>
        /// Negotiates the indicator.
        /// </summary>
        /// <param name="scanSettings">The scan settings.</param>
        public void NegotiateProgressIndicator(ScanSettings scanSettings) {
            try {
                if (scanSettings.ShowProgressIndicatorUi.HasValue) {
                    Capability.SetCapability(Capabilities.Indicators, scanSettings.ShowProgressIndicatorUi.Value, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        public void NegotiateResolution(ScanSettings scanSettings) {
            try {
                if (scanSettings.Resolution.Dpi.HasValue) {
                    int dpi = scanSettings.Resolution.Dpi.Value;
                    _ = Capability.SetBasicCapability(Capabilities.XResolution, dpi, TwainType.Fix32, _applicationId, SourceId);
                    _ = Capability.SetBasicCapability(Capabilities.YResolution, dpi, TwainType.Fix32, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        public void NegotiateTransferCount(ScanSettings scanSettings) {
            try {
                scanSettings.TransferCount = Capability.SetCapability(
                        Capabilities.XferCount,
                        scanSettings.TransferCount,
                        _applicationId,
                        SourceId);
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }
        }

        public bool Open(ScanSettings settings) {
            OpenSource();

            if (settings.AbortWhenNoPaperDetectable && !PaperDetectable) {
                throw new FeederEmptyException();
            }

            // Set whether or not to show progress window
            NegotiateProgressIndicator(settings);
            NegotiateTransferCount(settings);
            NegotiateFeeder(settings);
            NegotiateDuplex(settings);
            NegotiateLightPath(settings);

            if (settings.UseDocumentFeeder == true &&
                settings.Page != null) {
                NegotiatePageSize(settings);
                NegotiateOrientation(settings);
            }

            if (settings.Area != null) {
                _ = NegotiateArea(settings);
            }

            if (settings.Resolution != null) {
                NegotiateColour(settings);
                NegotiateResolution(settings);
            }

            // Configure automatic rotation and image border detection
            if (settings.Rotation != null) {
                NegotiateAutomaticRotate(settings);
                NegotiateAutomaticBorderDetection(settings);
            }

            return Enable(settings);
        }

        public void OpenSource() {
            TwainResult result = Twain32Native.DsmIdentity(
                   _applicationId,
                   IntPtr.Zero,
                   DataGroup.Control,
                   DataArgumentType.Identity,
                   Message.OpenDS,
                   SourceId);

            if (result != TwainResult.Success) {
                throw new TwainException("Error opening data source", result);
            }
        }

        protected void Dispose(bool disposing) {
            if (disposing) {
                Close();
            }
        }

        private readonly Identity _applicationId;

        private readonly IWindowsMessageHook _messageHook;

        ~DataSource() {
            Dispose(false);
        }

        private bool NegotiateArea(ScanSettings scanSettings) {
            AreaSettings area = scanSettings.Area;

            if (area == null) {
                return false;
            }

            try {
                Capability cap = new Capability(Capabilities.IUnits, TwainType.Int16, _applicationId, SourceId);
                if ((Units)cap.GetBasicValue().Int16Value != area.Units) {
                    _ = Capability.SetCapability(Capabilities.IUnits, (short)area.Units, _applicationId, SourceId);
                }
            }
            catch {
                // Do nothing if the data source does not support the requested capability
            }

            ImageLayout imageLayout = new ImageLayout {
                Frame = new Frame {
                    Left = new Fix32(area.Left),
                    Top = new Fix32(area.Top),
                    Right = new Fix32(area.Right),
                    Bottom = new Fix32(area.Bottom)
                }
            };

            TwainResult result = Twain32Native.DsImageLayout(
                _applicationId,
                SourceId,
                DataGroup.Image,
                DataArgumentType.ImageLayout,
                Message.Set,
                imageLayout);

            return result != TwainResult.Success ? throw new TwainException("DsImageLayout.GetDefault error", result) : true;
        }
    }
}