using FluentValidation;
using Orders.API.Contracts;
using Orders.Domain;

namespace Orders.API.Validation;

public abstract class PagingQueryValidator<T> : AbstractValidator<T> where T : PagingQuery
{
    protected PagingQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingQuery.MaxPageSize);
    }
}

public sealed class CustomerSearchQueryValidator : PagingQueryValidator<CustomerSearchQuery>
{
    public CustomerSearchQueryValidator() =>
        RuleFor(x => x.Search).MaximumLength(100);
}

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.CountryCode)
            .NotEmpty()
            .Length(2)
            .Must(code => SADC_Countries.IsSADC_Country(code))
            .WithMessage("'{PropertyValue}' is not a SADC country code (ISO 3166-1 alpha-2).");
    }
}