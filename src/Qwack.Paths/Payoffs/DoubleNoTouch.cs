using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Qwack.Core.Basic;
using Qwack.Core.Instruments;
using Qwack.Core.Models;
using Qwack.Math;
using Qwack.Math.Extensions;
using Qwack.Paths.Features;

namespace Qwack.Paths.Payoffs
{
    public class DoubleNoTouch : IPathProcess, IRequiresFinish, IAssetPathPayoff
    {
        private object _locker = new object();

        private readonly DateTime _obsStart;
        private readonly DateTime _obsEnd;
        private readonly double _barrierDown;
        private readonly double _barrierUp;
        private readonly string _discountCurve;
        private readonly Currency _ccy;
        private readonly DateTime _payDate;
        private readonly BarrierType _barrierType;
        private readonly BarrierSide _barrierSide;
        private readonly string _assetName;
        private int _assetIndex;
        private int[] _dateIndexes;
        private Vector<double>[] _results;
        private Vector<double> _notional;
        private Vector<double> _barrierUpVec;
        private Vector<double> _barrierDownVec;
        private Vector<double> _zero = new Vector<double>(0.0);
        private bool _isComplete;

        public string RegressionKey => _assetName;


        public DoubleNoTouch(string assetName, DateTime obsStart, DateTime obsEnd, double barrierDown, double barrierUp, string discountCurve, Currency ccy, DateTime payDate, double notional, BarrierType barrierType)
        {
            _obsStart = obsStart;
            _obsEnd = obsEnd;
            _barrierDown = barrierDown;
            _barrierUp = barrierUp;
            _discountCurve = discountCurve;
            _ccy = ccy;
            _payDate = payDate;

            _barrierType = barrierType;

            _assetName = assetName;
            _notional = new Vector<double>(notional);
            _barrierUpVec = new Vector<double>(barrierUp);
            _barrierDownVec = new Vector<double>(barrierDown);
        }

        public bool IsComplete => _isComplete;

        public IAssetInstrument AssetInstrument { get; private set; }


        public void Finish(IFeatureCollection collection)
        {
            var dims = collection.GetFeature<IPathMappingFeature>();
            _assetIndex = dims.GetDimension(_assetName);

            var dates = collection.GetFeature<ITimeStepsFeature>();
            
            var obsStart = dates.GetDateIndex(_obsStart);
            var obsEnd = dates.GetDateIndex(_obsEnd);
            _dateIndexes = Enumerable.Range(obsStart, (obsEnd - obsStart) + 1).ToArray();

            var engine = collection.GetFeature<IEngineFeature>();
            _results = new Vector<double>[engine.NumberOfPaths / Vector<double>.Count];
            _isComplete = true;
        }

        public void Process(IPathBlock block)
        {
            var blockBaseIx = block.GlobalPathIndex;

            var barrierDownHit = new Vector<double>(0);
            var barrierUpHit = new Vector<double>(0);
            var expiryValue = new Vector<double>(0);

            for (var path = 0; path < block.NumberOfPaths; path += Vector<double>.Count)
            {
                var steps = block.GetStepsForFactor(path, _assetIndex);
                var minValue = new Vector<double>(double.MaxValue);
                var maxValue = new Vector<double>(double.MinValue);
                for (var i = 0; i < _dateIndexes.Length; i++)
                {
                    minValue = Vector.Min(steps[_dateIndexes[i]], minValue);
                    maxValue = Vector.Max(steps[_dateIndexes[i]], maxValue);
                }
                barrierDownHit = Vector.Abs(Vector.ConvertToDouble(Vector.LessThan(minValue, _barrierDownVec)));
                barrierUpHit = Vector.ConvertToDouble(Vector.GreaterThan(maxValue, _barrierUpVec));


                if (_barrierType == BarrierType.KI)
                {
                    var barrierHit = Vector.BitwiseAnd(barrierDownHit, barrierUpHit);
                    var payoff = barrierHit * _notional;
                    var resultIx = (blockBaseIx + path) / Vector<double>.Count;
                    _results[resultIx] = payoff;
                }
                else //KO
                {
                    var barrierHit = Vector.BitwiseAnd(Vector<double>.One - barrierDownHit, Vector<double>.One - barrierUpHit);
                    var payoff = barrierHit * _notional;
                    var resultIx = (blockBaseIx + path) / Vector<double>.Count;
                    _results[resultIx] = payoff;
                }
            }
        }

        public void SetupFeatures(IFeatureCollection pathProcessFeaturesCollection)
        {
            var dates = pathProcessFeaturesCollection.GetFeature<ITimeStepsFeature>();
            dates.AddDate(_obsStart);
            dates.AddDate(_obsEnd);
        }

        public double AverageResult => _results.Select(x =>
        {
            var vec = new double[Vector<double>.Count];
            x.CopyTo(vec);
            return vec.Average();
        }).Average();

        public double[] ResultsByPath => _results.SelectMany(x => x.Values()).ToArray();

        public double ResultStdError => _results.SelectMany(x =>
        {
            var vec = new double[Vector<double>.Count];
            x.CopyTo(vec);
            return vec;
        }).StdDev();



        public CashFlowSchedule ExpectedFlows(IAssetFxModel model)
        {
            var ar = AverageResult;
            return new CashFlowSchedule
            {
                Flows = new List<CashFlow>
                {
                    new CashFlow
                    {
                        Fv = ar,
                        Pv = ar * model.FundingModel.Curves[_discountCurve].GetDf(model.BuildDate,_payDate),
                        Currency = _ccy,
                        FlowType =  FlowType.FixedAmount,
                        SettleDate = _payDate,
                        NotionalByYearFraction = 1.0
                    }
                }
            };
        }

        public CashFlowSchedule[] ExpectedFlowsByPath(IAssetFxModel model)
        {
            var df = model.FundingModel.Curves[_discountCurve].GetDf(model.BuildDate, _payDate);
            return ResultsByPath.Select(x => new CashFlowSchedule
            {
                Flows = new List<CashFlow>
                {
                    new CashFlow
                    {
                        Fv = x,
                        Pv = x * df,
                        Currency = _ccy,
                        FlowType =  FlowType.FixedAmount,
                        SettleDate = _payDate,
                        NotionalByYearFraction = 1.0
                    }
                }
            }).ToArray();

        }
    }
}
