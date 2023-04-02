using System;
using System.Drawing;

namespace PdfiumViewer
{
    public readonly struct PdfPoint : IEquatable<PdfPoint>
    {
        public static readonly PdfPoint Empty = new PdfPoint();

        public PdfPoint(int page, PointF location)
        {
            _page = page + 1;
            Location = location;
        }

        public bool IsValid => _page != 0;

        public PointF Location { get; }

        public int Page => _page - 1;

        public static bool operator !=(PdfPoint left, PdfPoint right)
        {
            return !left.Equals(right);
        }

        public static bool operator ==(PdfPoint left, PdfPoint right)
        {
            return left.Equals(right);
        }

        public bool Equals(PdfPoint other)
        {
            return
                Page == other.Page &&
                Location == other.Location;
        }

        public override bool Equals(object obj)
        {
            return
                obj is PdfPoint &&
                Equals((PdfPoint)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Page * 397) ^ Location.GetHashCode();
            }
        }

        // _page is offset by 1 so that Empty returns an invalid point.
        private readonly int _page;
    }
}