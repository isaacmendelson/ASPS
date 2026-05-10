using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Business.Migrations
{
    /// <summary>
    /// Seeds Israeli banks and major Israeli financial sites into SensitiveSites.
    /// Existing rows (e.g., bankleumi.co.il, *.paypal.com) are not touched —
    /// inserts are guarded by NOT EXISTS on DomainPattern.
    /// </summary>
    public partial class SeedIsraeliBanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each row: domainPattern, category, riskMultiplier
            // Both bare-domain and *. wildcard so subdomains like mto.X, online.X, login.X all match.
            var rows = new (string Domain, string Category, decimal Risk)[]
            {
                // Major Israeli banks
                ("bankhapoalim.co.il",         "Banking", 2.5m),
                ("*.bankhapoalim.co.il",       "Banking", 2.5m),
                ("hapoalim.co.il",             "Banking", 2.5m),
                ("*.hapoalim.co.il",           "Banking", 2.5m),
                ("mizrahi-tefahot.co.il",      "Banking", 2.5m),
                ("*.mizrahi-tefahot.co.il",    "Banking", 2.5m),
                ("discountbank.co.il",         "Banking", 2.5m),
                ("*.discountbank.co.il",       "Banking", 2.5m),
                ("fibi.co.il",                 "Banking", 2.5m),
                ("*.fibi.co.il",               "Banking", 2.5m),
                ("otsarhahayal.co.il",         "Banking", 2.5m),
                ("*.otsarhahayal.co.il",       "Banking", 2.5m),
                ("mercantile.co.il",           "Banking", 2.5m),
                ("*.mercantile.co.il",         "Banking", 2.5m),
                ("bankjerusalem.co.il",        "Banking", 2.5m),
                ("*.bankjerusalem.co.il",      "Banking", 2.5m),
                ("u-bank.net",                 "Banking", 2.5m),
                ("*.u-bank.net",               "Banking", 2.5m),
                ("pagi.co.il",                 "Banking", 2.5m),
                ("*.pagi.co.il",               "Banking", 2.5m),
                ("massad.co.il",               "Banking", 2.5m),
                ("*.massad.co.il",             "Banking", 2.5m),
                ("yahav.co.il",                "Banking", 2.5m),
                ("*.yahav.co.il",              "Banking", 2.5m),
                ("*.bankleumi.co.il",          "Banking", 2.5m), // bare bankleumi already seeded earlier

                // Major Israeli credit-card / payment
                ("isracard.co.il",             "Banking", 2.5m),
                ("*.isracard.co.il",           "Banking", 2.5m),
                ("max.co.il",                  "Banking", 2.5m),
                ("*.max.co.il",                "Banking", 2.5m),
                ("cal-online.co.il",           "Banking", 2.5m),
                ("*.cal-online.co.il",         "Banking", 2.5m),
                ("leumi-card.co.il",           "Banking", 2.5m),
                ("*.leumi-card.co.il",         "Banking", 2.5m),
                ("bit.co.il",                  "Banking", 2.5m),
                ("*.bit.co.il",                "Banking", 2.5m),

                // Crypto exchanges (additions)
                ("bits-of-gold.com",           "Crypto",  3.0m),
                ("*.bits-of-gold.com",         "Crypto",  3.0m),
                ("kraken.com",                 "Crypto",  3.0m),
                ("*.kraken.com",               "Crypto",  3.0m),
                ("bitstamp.net",               "Crypto",  3.0m),
                ("*.bitstamp.net",             "Crypto",  3.0m),

                // Israeli government
                ("gov.il",                     "Government", 2.0m),
                ("btl.gov.il",                 "Government", 2.0m),
                ("taxes.gov.il",               "Government", 2.0m),
            };

            foreach (var (domain, category, risk) in rows)
            {
                var safeDomain = domain.Replace("'", "''");
                var safeCategory = category.Replace("'", "''");
                migrationBuilder.Sql(
                    $@"INSERT INTO SensitiveSites (DomainPattern, Category, RiskMultiplier, IsActive, DateCreated, DateModified)
                       SELECT '{safeDomain}', '{safeCategory}', {risk.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()
                       FROM dual
                       WHERE NOT EXISTS (
                           SELECT 1 FROM SensitiveSites WHERE DomainPattern = '{safeDomain}'
                       );");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Soft-rollback: only delete rows we inserted (by their DomainPattern values).
            var domains = new[]
            {
                "bankhapoalim.co.il", "*.bankhapoalim.co.il", "hapoalim.co.il", "*.hapoalim.co.il",
                "mizrahi-tefahot.co.il", "*.mizrahi-tefahot.co.il",
                "discountbank.co.il", "*.discountbank.co.il",
                "fibi.co.il", "*.fibi.co.il",
                "otsarhahayal.co.il", "*.otsarhahayal.co.il",
                "mercantile.co.il", "*.mercantile.co.il",
                "bankjerusalem.co.il", "*.bankjerusalem.co.il",
                "u-bank.net", "*.u-bank.net",
                "pagi.co.il", "*.pagi.co.il",
                "massad.co.il", "*.massad.co.il",
                "yahav.co.il", "*.yahav.co.il",
                "*.bankleumi.co.il",
                "isracard.co.il", "*.isracard.co.il",
                "max.co.il", "*.max.co.il",
                "cal-online.co.il", "*.cal-online.co.il",
                "leumi-card.co.il", "*.leumi-card.co.il",
                "bit.co.il", "*.bit.co.il",
                "bits-of-gold.com", "*.bits-of-gold.com",
                "kraken.com", "*.kraken.com",
                "bitstamp.net", "*.bitstamp.net",
                "gov.il", "btl.gov.il", "taxes.gov.il",
            };
            var inList = string.Join(",", System.Linq.Enumerable.Select(domains, d => $"'{d.Replace("'", "''")}'"));
            migrationBuilder.Sql($"DELETE FROM SensitiveSites WHERE DomainPattern IN ({inList});");
        }
    }
}
