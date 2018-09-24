using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Qwack.Core.Basic;
using Qwack.Core.Instruments;
using Qwack.Core.Instruments.Asset;
using Qwack.Math;
using Qwack.Paths;
using Qwack.Paths.Features;

namespace Qwack.Models.MCModels
{
    public class AssetPathPayoff : IPathProcess, IRequiresFinish
    {
        private List<DateTime> _asianDates;
        private readonly double _strike;
        private readonly double _notional;
        private readonly string _assetName;
        private int _assetIndex;
        private int[] _dateIndexes;
        private List<Vector<double>> _results = new List<Vector<double>>();
        private bool _isComplete;
        private OptionType _optionType;

        public AssetPathPayoff(IAssetInstrument assetInstrument)
        {
            AssetInstrument = assetInstrument;
            switch (AssetInstrument)
            {
                case AsianOption ao:
                    _asianDates = ao.FixingDates.ToList();
                    _strike = ao.Strike;
                    _notional = ao.Notional * (ao.Direction == TradeDirection.Long ? 1.0 : -1.0);
                    _optionType = ao.CallPut;
                    break;
                case AsianSwap asw:
                    _asianDates = asw.FixingDates.ToList();
                    _strike = asw.Strike;
                    _notional = asw.Notional * (asw.Direction == TradeDirection.Long ? 1.0 : -1.0);
                    _optionType = OptionType.Swap;
                    break;
                case AsianSwapStrip asws:
                    _asianDates = asws.Swaplets.SelectMany(s => s.FixingDates).Distinct().OrderBy(d => d).ToList();
                    _strike = asws.Swaplets.First().Strike;
                    _notional = asws.Swaplets.First().Notional * (asws.Swaplets.First().Direction == TradeDirection.Long ? 1.0 : -1.0);
                    _optionType = OptionType.Swap;
                    break;
                case EuropeanOption eo:
                    _asianDates = new List<DateTime> { eo.ExpiryDate };
                    _strike = eo.Strike;
                    _notional = eo.Notional * (eo.Direction == TradeDirection.Long ? 1.0 : -1.0);
                    _optionType = eo.CallPut;
                    break;
                case Forward f:
                    _asianDates = new List<DateTime> { f.ExpiryDate };
                    _strike = f.Strike;
                    _notional = f.Notional * (f.Direction == TradeDirection.Long ? 1.0 : -1.0);
                    _optionType = OptionType.Swap;
                    break;
                case AsianBasisSwap abs:
                    throw new NotSupportedException("Multi-asset payoffs not yet supported");
            }

            _assetName = AssetInstrument.AssetIds.First();
        }

        public bool IsComplete => _isComplete;

        public IAssetInstrument AssetInstrument { get; }

        public void Finish(FeatureCollection collection)
        {
            var dims = collection.GetFeature<IPathMappingFeature>();
            _assetIndex = dims.GetDimension(_assetName);

            var dates = collection.GetFeature<ITimeStepsFeature>();
            _dateIndexes = new int[_asianDates.Count];
            for (var i = 0; i < _asianDates.Count; i++)
            {
                _dateIndexes[i] = dates.GetDateIndex(_asianDates[i]);
            }
            _isComplete = true;
        }

        public void Process(PathBlock block)
        {
            for (var path = 0; path < block.NumberOfPaths; path += Vector<double>.Count)
            {
                var steps = block.GetStepsForFactor(path, _assetIndex);
                var finalValues = new Vector<double>(0.0);
                for (var i = 0; i < _dateIndexes.Length; i++)
                {
                    finalValues += steps[_dateIndexes[i]];
                }

                switch (_optionType)
                {
                    case OptionType.Call:
                        finalValues = ((finalValues / new Vector<double>(_dateIndexes.Length)- new Vector<double>(_strike)));
                        finalValues = Vector.Max(new Vector<double>(0), finalValues) * new Vector<double>(_notional);
                        break;
                    case OptionType.Put:
                        finalValues = (new Vector<double>(_strike)) - (finalValues / new Vector<double>(_dateIndexes.Length));
                        finalValues = Vector.Max(new Vector<double>(0), finalValues) * new Vector<double>(_notional);
                        break;
                    case OptionType.Swap:
                        finalValues = ((finalValues / new Vector<double>(_dateIndexes.Length) - new Vector<double>(_strike)));
                        finalValues = finalValues * new Vector<double>(_notional);
                        break;
                }

                _results.Add(finalValues);
            }
        }

        public void SetupFeatures(FeatureCollection pathProcessFeaturesCollection)
        {
            var dates = pathProcessFeaturesCollection.GetFeature<ITimeStepsFeature>();
            dates.AddDates(_asianDates);
        }

        public double AverageResult => _results.Select(x =>
        {
            var vec = new double[Vector<double>.Count];
            x.CopyTo(vec);
            return vec.Average();
        }).Average();

        public double ResultStdError => _results.SelectMany(x =>
        {
            var vec = new double[Vector<double>.Count];
            x.CopyTo(vec);
            return vec;
        }).StdDev();
    }
}
