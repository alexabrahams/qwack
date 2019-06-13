using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using Qwack.Core.Basic;
using Qwack.Core.Curves;
using Qwack.Core.Instruments.Asset;
using Qwack.Core.Models;
using Qwack.Dates;
using Xunit;

namespace Qwack.Core.Tests.Instruments
{
    public class BackPricingOptionFacts
    {
        [Fact]
        public void BackPricingOptionFact()
        {
            var orgin = new DateTime(2019, 06, 12);
            var usd = TestProviderHelper.CurrencyProvider.GetCurrency("USD");
            var fixDates = new[] { orgin };
            var x = new BackPricingOption()
            {
                AssetId = "QS",
                CallPut = OptionType.C,
                DiscountCurve = "X",
                FixingDates = fixDates,
                FxConversionType = FxConversionType.None,
                Notional = 1,
                PaymentCurrency = usd,
                StartDate = orgin,
                EndDate = orgin,
                SpotLag = 0.Bd(),
                SpotLagRollType = RollType.F,
            };

            var fakeModel = new Mock<IAssetFxModel>();
            var c = new ConstantPriceCurve(100, DateTime.Today, TestProviderHelper.CurrencyProvider) { Currency = usd };
            fakeModel.Setup(xx => xx.GetPriceCurve(It.IsAny<string>(), null)).Returns(c);

            Assert.Equal(usd, x.Currency);
            Assert.Equal(usd, x.PaymentCurrency);
            var a = x.AssetIds;
            Assert.Contains("QS", a);
            Assert.Single(x.IrCurves(fakeModel.Object));
            Assert.Equal(FxConversionType.None, x.FxType(fakeModel.Object));
            Assert.Equal(orgin, x.LastSensitivityDate);

            var pf = x.PastFixingDates(orgin.AddDays(1));
            Assert.Contains("QS", pf.Keys);

            Assert.True(x == x);
            var y = (BackPricingOption)x.Clone();
            y.TradeId = "xxx";
            Assert.False(x == y);
            Assert.Equal(string.Empty, x.FxPair(fakeModel.Object));

            Assert.Throws<InvalidOperationException>(() => x.SetStrike(0));

        }
    }
}
