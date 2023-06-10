using System;
using System.Diagnostics;

namespace Tesseract
{
    public abstract class DisposableBase : IDisposable
    {
        public event EventHandler<EventArgs> Disposed;

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            Dispose(true);

            IsDisposed = true;
            GC.SuppressFinalize(this);

            Disposed?.Invoke(this, EventArgs.Empty);
        }

        protected DisposableBase()
        { IsDisposed = false; }

        protected abstract void Dispose(bool disposing);

        protected virtual void VerifyNotDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(ToString());
            }
        }

        private static readonly TraceSource trace = new TraceSource("Tesseract");

        ~DisposableBase()
        {
            Dispose(false);
            trace.TraceEvent(TraceEventType.Warning, 0, "{0} was not disposed off.", this);
        }
    }
}