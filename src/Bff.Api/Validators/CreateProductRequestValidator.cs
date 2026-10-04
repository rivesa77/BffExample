namespace Bff.Api.Validators;

using Bff.Api.Requests;
using FluentValidation;
using FluentValidation.Results;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres.");
        RuleFor(request => request.Description)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(1000).WithMessage("La descripción no puede superar los 1000 caracteres.");
        RuleFor(request => request.Price)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("El precio es obligatorio.")
            .GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.");
        RuleFor(request => request.Currency)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La moneda es obligatoria.")
            .Matches("\\A[A-Z]{3}\\z").WithMessage("La moneda debe contener exactamente tres letras mayúsculas.");
        RuleFor(request => request.InitialStock)
            .GreaterThanOrEqualTo(0).WithMessage("Las existencias iniciales no pueden ser negativas.");
    }

    protected override bool PreValidate(ValidationContext<CreateProductRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
            return true;

        result.Errors.Add(new ValidationFailure("Request", "Los datos del producto son obligatorios."));
        return false;
    }
}
