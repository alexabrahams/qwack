using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static System.Math;
using Qwack.Math;

namespace Qwack.Math.Interpolation
{
    public class NonContinuousInterpolator : IInterpolator1D
    {
        const double xBump = 1e-10;

        private readonly double[] _upperBounds;
        private readonly IInterpolator1D[] _interpolatorSegments;

        public NonContinuousInterpolator()
        {

        }

        public NonContinuousInterpolator(double[] upperBounds, IInterpolator1D[] interpolatorSegments)
        {
            _upperBounds = upperBounds;
            _interpolatorSegments = interpolatorSegments;
        }

        private int FindUpperBound(double t)
        {
            var index = Array.BinarySearch(_upperBounds, t);
            if (index < 0)
            {
                index = ~index;
            }

            return index;
        }

        public IInterpolator1D Bump(int pillar, double delta, bool updateInPlace = false) => throw new NotImplementedException();
        public double FirstDerivative(double t)
        {
            var k = FindUpperBound(t);
            if (k == _interpolatorSegments.Length)
                k--;
            return _interpolatorSegments[k].FirstDerivative(t);
        }

        public double Interpolate(double t)
        {
            var k = FindUpperBound(t);
            if (k == _interpolatorSegments.Length)
                k--;
            return _interpolatorSegments[k].Interpolate(t);
        }
    

        public double SecondDerivative(double t)
        {
            var k = FindUpperBound(t);
            if (k == _interpolatorSegments.Length)
                k--;
            return _interpolatorSegments[k].SecondDerivative(t);
        }

        public IInterpolator1D UpdateY(int pillar, double newValue, bool updateInPlace = false) => throw new NotImplementedException();
        public double[] Sensitivity(double t) => throw new NotImplementedException();
    }
}
