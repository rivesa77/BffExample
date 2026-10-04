namespace Bff.Api.Validators;

using FluentValidation;

public sealed class ProductIdValidator : AbstractValidator<int>
{
    public ProductIdValidator()
    {
        RuleFor(id => id)
            .GreaterThan(0)
            .OverridePropertyName("Id")
            .WithMessage("El id debe ser mayor que cero.");
    }
}
