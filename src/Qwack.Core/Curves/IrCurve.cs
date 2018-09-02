using System;
using System.Collections.Generic;
using System.Linq;
using Qwack.Core.Basic;
using Qwack.Core.Descriptors;
using Qwack.Dates;
using Qwack.Math.Interpolation;
using static System.Math;

namespace Qwack.Core.Curves
{
    public class IrCurve : ICurve
    {
        private DateTime _buildDate;
        private readonly DateTime[] _pillars;
        private readonly double[] _rates;
        private readonly DayCountBasis _basis = DayCountBasis.Act_365F;
        private readonly IInterpolator1D _interpolator;
        private readonly Interpolator1DType _interpKind;
        private readonly string _name;

        public IrCurve(DateTime[] pillars, double[] rates, DateTime buildDate, string name, Interpolator1DType interpKind)
        {
            _interpKind = interpKind;
            _pillars = new DateTime[pillars.Length];
            pillars.CopyTo(_pillars,0);
            _rates = new double[_pillars.Length];

            var pillarsD = new double[_pillars.Length];
            _buildDate = buildDate;

            for (var i = 0; i < pillars.Length; i++)
            {
                pillarsD[i] = buildDate.CalculateYearFraction(pillars[i], _basis);
                _rates[i] = rates[i];
            }

            _interpolator = InterpolatorFactory.GetInterpolator(pillarsD.ToArray(), _rates.ToArray(), interpKind, isSorted: true, noCopy: true);
            _name = name;
        }
        
        public DateTime BuildDate => _buildDate;
        public string Name => _name;
        public int NumberOfPillars => _pillars.Length;
        public DateTime[] PillarDates => _pillars;
        public Interpolator1DType InterpolatorType => _interpKind;
        public int SolveStage { get; set; }
        public DayCountBasis Basis => _basis;
        public Currency Currency;

        public List<MarketDataDescriptor> Descriptors => new List<MarketDataDescriptor>()
            {
                    new DiscountCurveDescriptor {
                        CollateralSpec = Name.Split('.').Last().Trim("[]".ToCharArray()),
                        Currency = Currency,
                        Name =Name,
                        ValDate =BuildDate}
            };
        public List<MarketDataDescriptor> Dependencies => new List<MarketDataDescriptor>();

        public double GetDf(DateTime startDate, DateTime endDate)
        {
            var ts = _buildDate.CalculateYearFraction(startDate, _basis);
            var te = _buildDate.CalculateYearFraction(endDate, _basis);
            var rateS = GetRate(ts);
            var rateE = GetRate(te);
            var dfS = Exp(-rateS * ts);
            var dfE = Exp(-rateE * te);
            return dfE / dfS;
        }
        public double GetRate(DateTime valueDate)
        {
            var T = _buildDate.CalculateYearFraction(valueDate, _basis);
            return GetRate(T);
        }

        public double GetRate(double T) => _interpolator.Interpolate(T);

        public double GetRate(int pillarIx) => _rates[pillarIx];

        public double GetForwardRate(DateTime startDate, DateTime endDate, RateType rateType, double tbasis)
        {
            var ccRate = GetForwardRate(startDate, endDate);
            var output = -1.0;

            var t365 = startDate.CalculateYearFraction(endDate, _basis);
            var cf = Exp(ccRate * t365);

            switch (rateType)
            {
                case RateType.Exponential:
                    output = Log(cf) / tbasis;
                    break;
                case RateType.Linear:
                    output = (cf - 1.0) / tbasis;
                    break;
                case RateType.DailyCompounded:
                case RateType.MonthlyCompounded:
                case RateType.YearlyCompounded:
                    throw new NotImplementedException();
            }
            return output;
        }
        public double GetForwardRate(DateTime startDate, DateTime endDate, RateType rateType, DayCountBasis basis)
        {
            var tbas = startDate.CalculateYearFraction(endDate, basis);
            return GetForwardRate(startDate, endDate, rateType, tbas);
        }

        public double GetForwardRate(DateTime startDate, DateTime endDate)
        {
            var te = _buildDate.CalculateYearFraction(endDate, _basis);
            var ts = _buildDate.CalculateYearFraction(startDate, _basis);
            var re = _interpolator.Interpolate(te);
            var rs = _interpolator.Interpolate(ts);
            var dFe = Exp(-te * re);
            var dFs = Exp(-ts * rs);

            var q = (1 / dFe) / (1 / dFs);

            return Log(q) / (te - ts);
        }

        public double[] GetRates() => _rates.ToArray();

        public double Pv(double fv, DateTime payDate)
        {
            var T = _buildDate.CalculateYearFraction(payDate, _basis);
            var rate = GetRate(T);
            var df = Exp(-rate * T);
            return fv * df;
        }
        
        public IrCurve BumpRate(int pillarIx, double delta, bool mutate)
        {
            if (mutate)
            {
                _rates[pillarIx] += delta;
                _interpolator.UpdateY(pillarIx, _rates[pillarIx], true);
                return this;
            }
            else
            {
                var returnCurve = new IrCurve(_pillars.ToArray(), _rates.Select((r, ix) => ix == pillarIx ? r + delta : r).ToArray(), _buildDate, _name, _interpKind);
                return returnCurve;
            }
        }

        public ICurve SetRate(int pillarIx, double rate, bool mutate)
        {
            if (mutate)
            {
                _rates[pillarIx] = rate;
                _interpolator.UpdateY(pillarIx, rate, true);
                return this;
            }
            else
            {
                var returnCurve = new IrCurve(_pillars.ToArray(), _rates.Select((r, ix) => ix == pillarIx ? rate : r).ToArray(), _buildDate, _name, _interpKind);
                return returnCurve;
            }

        }

        public double[] GetSensitivity(DateTime valueDate)
        {
            var T = _buildDate.CalculateYearFraction(valueDate, _basis);
            return _interpolator.Sensitivity(T);
        }
    }
}
