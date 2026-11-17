using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Orders.Domain
{
    public static class SADC_Countries
    {
        private static readonly Dictionary<string, string[]> CurrenciesByCountry = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AO"] = ["AOA"], // Angola
            ["BW"] = ["BWP"], // Botswana
            ["KM"] = ["KMF"], // Comoros
            ["CD"] = ["CDF"], // DRC
            ["SZ"] = ["SZL"], // Eswatini
            ["LS"] = ["LSL"], // Lesotho
            ["MG"] = ["MGA"], // Madagascar
            ["MW"] = ["MWK"], // Malawi
            ["MU"] = ["MUR"], // Mauritius
            ["MZ"] = ["MZN"], // Mozambique
            ["NA"] = ["NAD"], // Namibia
            ["SC"] = ["SCR"], // Seychelles
            ["ZA"] = ["ZAR"], // South Africa
            ["TZ"] = ["TZS"], // Tanzania
            ["ZM"] = ["ZMW"], // Zambia
            ["ZW"] = ["ZWL", "USD"], // Zimbabwe
        };

        private static readonly HashSet<string> CmaRandCountries =  new(StringComparer.OrdinalIgnoreCase) { "NA", "LS", "SZ" };

        public static IReadOnlyCollection<string> Countries => CurrenciesByCountry.Keys;

        public static bool IsSADC_Country(string? countryCode) => !string.IsNullOrWhiteSpace(countryCode) && CurrenciesByCountry.ContainsKey(countryCode);

        public static bool IsCurrencyAllowed(string countryCode, string currencyCode, bool allowRandInCma = false)
        {
            if (!CurrenciesByCountry.TryGetValue(countryCode, out var currencies))
                return false;

            if (currencies.Contains(currencyCode, StringComparer.OrdinalIgnoreCase))
                return true;

            return allowRandInCma
                   && CmaRandCountries.Contains(countryCode)
                   && string.Equals(currencyCode, "ZAR", StringComparison.OrdinalIgnoreCase);
        }
    }
}