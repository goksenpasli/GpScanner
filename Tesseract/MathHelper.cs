﻿using System;

namespace Tesseract
{
    public static class MathHelper
    {
        /// <summary>
        /// Calculates the smallest integer greater than the quotant of dividend and divisor.
        /// </summary>
        /// <see href="http://stackoverflow.com/questions/921180/how-can-i-ensure-that-a-division-of-integers-is-always-rounded-up"/>
        public static int DivRoundUp(int dividend, int divisor)
        {
            int result = dividend / divisor;

            return dividend % divisor != 0 && (divisor > 0) == (dividend > 0) ? result + 1 : result;
        }

        /// <summary>
        /// Convert a degrees to radians.
        /// </summary>
        /// <param name="angleInDegrees"></param>
        /// <returns></returns>
        public static float ToRadians(float angleInDegrees)
        {
            return (float)ToRadians((double)angleInDegrees);
        }

        /// <summary>
        /// Convert a degrees to radians.
        /// </summary>
        /// <param name="angleInDegrees"></param>
        /// <returns></returns>
        public static double ToRadians(double angleInDegrees)
        {
            return angleInDegrees * Math.PI / 180.0;
        }
    }
}