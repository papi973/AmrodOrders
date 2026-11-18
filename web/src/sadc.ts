// Mirrors Orders.Domain.Sadc. (Trade-off: duplicated for speed; a GET /api/reference/sadc endpoint would remove it.)
export const SADC: Record<string, { name: string; currencies: string[] }> = {
    AO: { name: "Angola", currencies: ["AOA"] },
    BW: { name: "Botswana", currencies: ["BWP"] },
    KM: { name: "Comoros", currencies: ["KMF"] },
    CD: { name: "DR Congo", currencies: ["CDF"] },
    SZ: { name: "Eswatini", currencies: ["SZL"] },
    LS: { name: "Lesotho", currencies: ["LSL"] },
    MG: { name: "Madagascar", currencies: ["MGA"] },
    MW: { name: "Malawi", currencies: ["MWK"] },
    MU: { name: "Mauritius", currencies: ["MUR"] },
    MZ: { name: "Mozambique", currencies: ["MZN"] },
    NA: { name: "Namibia", currencies: ["NAD"] },
    SC: { name: "Seychelles", currencies: ["SCR"] },
    ZA: { name: "South Africa", currencies: ["ZAR"] },
    TZ: { name: "Tanzania", currencies: ["TZS"] },
    ZM: { name: "Zambia", currencies: ["ZMW"] },
    ZW: { name: "Zimbabwe", currencies: ["ZWL", "USD"] },
};

const CMA_RAND_COUNTRIES = ["NA", "LS", "SZ"]; // matches Ordering:AllowRandInCma = true

export function currenciesFor(countryCode: string): string[] {
    const own = SADC[countryCode]?.currencies ?? [];
    return CMA_RAND_COUNTRIES.includes(countryCode) ? [...own, "ZAR"] : own;
}

export function formatMoney(amount: number, currency: string): string {
    return new Intl.NumberFormat("en-ZA", { style: "currency", currency }).format(amount);
}