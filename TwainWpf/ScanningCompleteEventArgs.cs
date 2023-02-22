using System;

namespace TwainWpf {
    public class ScanningCompleteEventArgs : EventArgs {
        public ScanningCompleteEventArgs(Exception exception) {
            Exception = exception;
        }

        public Exception Exception { get; }
    }
}