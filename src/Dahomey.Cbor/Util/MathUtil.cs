using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Util
{
    public static class MathUtil
    {
        /// <summary>
        /// Multiplies a floating point value x by the number 2 raised to the exp power.
        /// </summary>
        /// <param name="x">floating point value</param>
        /// <param name="exp">integer value</param>
        /// <returns></returns>
        public static double LdExp(double x, int exp)
        {
            return x * Math.Pow(2, exp);
        }
    }
}
