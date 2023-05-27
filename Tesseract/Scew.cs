namespace Tesseract
{
    public readonly struct Scew
    {
        public Scew(float angle, float confidence)
        {
            Angle = angle;
            Confidence = confidence;
        }

        public float Angle { get; }

        public float Confidence { get; }

        #region ToString
        public override string ToString() { return $"Scew: {Angle} [conf: {Confidence}]"; }
        #endregion ToString

        #region Equals and GetHashCode implementation
        public static bool operator !=(Scew lhs, Scew rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator ==(Scew lhs, Scew rhs)
        {
            return lhs.Equals(rhs);
        }

        public override bool Equals(object obj) { return obj is Scew && Equals((Scew)obj); }

        public bool Equals(Scew other) { return Confidence == other.Confidence && Angle == other.Angle; }

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