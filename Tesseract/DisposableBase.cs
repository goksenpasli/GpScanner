using System;
using System.Diagnostics;

namespace Tesseract
{
    public abstract class DisposableBase : IDisposable
    {
        protected DisposableBase() { IsDisposed = false; }

        ~DisposableBase()
        {
            Dispose(false);
            trace.TraceEvent(TraceEventType.Warning, 0, "{0} was not disposed off.", this);
        }

        public event EventHandler<EventArgs> Disposed;

        public void Dispose()
        {
            Dispose(true);

            IsDisposed = true;
            GC.SuppressFinalize(this);

            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public bool IsDisposed { get; private set; }

        protected abstract void Dispose(bool disposing);

        protected virtual void VerifyNotDisposed()
        {
            if(IsDisposed)
            {
                throw new ObjectDisposedException(ToString());
            }
        }

        private static readonly TraceSource trace = new TraceSource("Tesseract");
    }
}