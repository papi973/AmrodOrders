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

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.LineItems).NotEmpty().WithMessage("At least one line item is required.");
        RuleFor(x => x.LineItems.Count).LessThanOrEqualTo(100).When(x => x.LineItems is not null)
            .WithMessage("An order may have at most 100 line items.");

        RuleForEach(x => x.LineItems).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductSku).NotEmpty().MaximumLength(64);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        });
    }
}

public sealed class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator() =>
        RuleFor(x => x.Status).NotNull().IsInEnum();
}

public sealed class OrderSearchQueryValidator : PagingQueryValidator<OrderSearchQuery>
{
    public OrderSearchQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Sort)
            .Must(s => OrderSearchQuery.AllowedSorts.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"sort must be one of: {string.Join(", ", OrderSearchQuery.AllowedSorts)}");
    }
}