using System;

namespace Tesseract
{
    public readonly struct Rect : IEquatable<Rect>
    {
        public static readonly Rect Empty = new Rect();
        #region ToString
        public override string ToString() => $"[Rect X={X1}, Y={Y1}, Width={Width}, Height={Height}]";
        #endregion ToString

        #region Constructors + Factory Methods
        public Rect(int x, int y, int width, int height)
        {
            X1 = x;
            Y1 = y;
            Width = width;
            Height = height;
        }

        public static Rect FromCoords(int x1, int y1, int x2, int y2) => new Rect(x1, y1, x2 - x1, y2 - y1);
        #endregion Constructors + Factory Methods

        #region Properties
        public int Height { get; }

        public int Width { get; }

        public int X1 { get; }

        public int X2 => X1 + Width;

        public int Y1 { get; }

        public int Y2 => Y1 + Height;
        #endregion Properties

        #region Equals and GetHashCode implementation
        public static bool operator !=(Rect lhs, Rect rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator ==(Rect lhs, Rect rhs)
        {
            return lhs.Equals(rhs);
        }

        public override bool Equals(object obj) => obj is Rect && Equals((Rect)obj);

        public bool Equals(Rect other) => X1 == other.X1 && Y1 == other.Y1 && Width == other.Width && Height == other.Height;

        public override int GetHashCode()
        {
            int hashCode = 0;
            unchecked
            {
                hashCode += 1000000007 * X1.GetHashCode();
                hashCode += 1000000009 * Y1.GetHashCode();
                hashCode += 1000000021 * Width.GetHashCode();
                hashCode += 1000000033 * Height.GetHashCode();
            }

            return hashCode;
        }
        #endregion Equals and GetHashCode implementation
    }
}