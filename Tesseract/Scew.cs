namespace Tesseract
{
    public readonly struct Scew
    {
        public Scew(float angle, float confidence)
        {
            Angle = angle;
            Confidence = confidence;
        }

        #region ToString

        public override string ToString() => $"Scew: {Angle} [conf: {Confidence}]";

        #endregion ToString

        public float Angle { get; }

        public float Confidence { get; }

        #region Equals and GetHashCode implementation

        public static bool operator !=(Scew lhs, Scew rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator ==(Scew lhs, Scew rhs)
        {
            return lhs.Equals(rhs);
        }

        public override bool Equals(object obj) => obj is Scew && Equals((Scew)obj);

        public bool Equals(Scew other) => Confidence == other.Confidence && Angle == other.Angle;

        public override int GetHashCode()
        {
            int hashCode = 0;
            unchecked
            {
                hashCode += 1000000007 * Angle.GetHashCode();
                hashCode += 1000000009 * Confidence.GetHashCode();
            }

            return hashCode;
        }

        #endregion Equals and GetHashCode implementation
    }
}