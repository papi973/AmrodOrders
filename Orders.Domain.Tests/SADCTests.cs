using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain.Tests
{
    public class SADCTests
    {
        [Theory]
        [InlineData("ZA", "ZAR", true)]
        [InlineData("BW", "BWP", true)]
        [InlineData("ZW", "ZWL", true)]
        [InlineData("ZW", "USD", true)]
        [InlineData("za", "zar", true)]   // case-insensitive
        [InlineData("ZA", "USD", false)]
        [InlineData("BW", "ZAR", false)]  // Botswana is not in the CMA
        [InlineData("KE", "KES", false)]  // Kenya is not SADC
        public void Validates_country_currency_pairing(string country, string currency, bool expected) =>
       Assert.Equal(expected, SADC_Countries.IsCurrencyAllowed(country, currency));

        [Theory]
        [InlineData("NA")]
        [InlineData("LS")]
        [InlineData("SZ")]
        public void Rand_is_accepted_in_CMA_only_when_enabled(string country)
        {
            Assert.False(SADC_Countries.IsCurrencyAllowed(country, "ZAR"));
            Assert.True(SADC_Countries.IsCurrencyAllowed(country, "ZAR", allowRandInCma: true));
        }

        [Fact]
        public void Has_all_16_member_states() => Assert.Equal(16, SADC_Countries.Countries.Count);
    }
}
