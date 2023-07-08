﻿namespace Tesseract
{
    /// <summary>
    /// Represents the parameters for a sweep search used by scew algorithms.
    /// </summary>
    public struct ScewSweep
    {
        public static ScewSweep Default = new ScewSweep(DefaultReduction);

        #region Factory Methods + Constructor
        public ScewSweep(int reduction = DefaultReduction, float range = DefaultRange, float delta = DefaultDelta)
        {
            Reduction = reduction;
            Range = range;
            Delta = delta;
        }
        #endregion Factory Methods + Constructor

        #region Constants and Fields
        public const float DefaultDelta = 1.0F;

        public const float DefaultRange = 7.0F;

        public const int DefaultReduction = 4;
        #endregion Constants and Fields

        #region Properties
        public float Delta { get; }

        public float Range { get; }

        public int Reduction { get; }
        #endregion Properties
    }
}