using System;
using System.Collections.Generic;
using System.IO;

namespace PdfiumViewer
{
    internal static class StreamManager
    {
        public static Stream Get(int id)
        {
            lock (_syncRoot)
            {
                _ = _files.TryGetValue(id, out Stream stream);
                return stream;
            }
        }

        public static int Register(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            lock (_syncRoot)
            {
                int id = _nextId++;
                _files.Add(id, stream);
                return id;
            }
        }

        public static void Unregister(int id)
        {
            lock (_syncRoot)
            {
                _ = _files.Remove(id);
            }
        }

        private static readonly Dictionary<int, Stream> _files = new Dictionary<int, Stream>();

        private static readonly object _syncRoot = new object();

        private static int _nextId = 1;
    }
}