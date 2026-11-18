using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain
{
    public class Customer
    {
        private Customer() { }

        public Customer(string name, string email, string countryCode)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("customer.name_required", "Name is required.");
            if (string.IsNullOrWhiteSpace(email))
                throw new DomainException("customer.email_required", "Email is required.");

            var country = countryCode?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!SADC_Countries.IsSADC_Country(country))
                throw new DomainException("customer.country_not_sadc", $"'{countryCode}' is not a SADC country code.");

            Id = Guid.NewGuid();
            Name = name.Trim();
            Email = email.Trim().ToLowerInvariant();
            CountryCode = country;
            CreatedAt = DateTime.UtcNow;
        }

        public Guid Id { get; private set; }
        public string Name { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        public string CountryCode { get; private set; } = null!;
        public DateTime CreatedAt { get; private set; }
    }
}
