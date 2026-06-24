using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using FluentValidation;

namespace Commerce.Application.Catalog.Validators;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Key is required.")
            .Must(CatalogKey.IsValid)
            .WithMessage("Key must be 2–64 chars, alphanumeric or - _ .");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000);
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000);
    }
}

public sealed class CreateFeatureRequestValidator : AbstractValidator<CreateFeatureRequest>
{
    public CreateFeatureRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().Must(CatalogKey.IsValid)
            .WithMessage("Key must be 2–64 chars, alphanumeric or - _ .");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.FeatureType).IsInEnum();
    }
}

public sealed class UpdateFeatureRequestValidator : AbstractValidator<UpdateFeatureRequest>
{
    public UpdateFeatureRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.FeatureType).IsInEnum();
    }
}

public sealed class CreatePlanRequestValidator : AbstractValidator<CreatePlanRequest>
{
    public CreatePlanRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().Must(CatalogKey.IsValid)
            .WithMessage("Key must be 2–64 chars, alphanumeric or - _ .");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.BillingInterval).IsInEnum();
        RuleFor(x => x.TrialDays).InclusiveBetween(0, 365).When(x => x.TrialDays.HasValue);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000);
    }
}

public sealed class UpdatePlanRequestValidator : AbstractValidator<UpdatePlanRequest>
{
    public UpdatePlanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.BillingInterval).IsInEnum();
        RuleFor(x => x.TrialDays).InclusiveBetween(0, 365).When(x => x.TrialDays.HasValue);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000);
    }
}

public sealed class AddPlanFeatureRequestValidator : AbstractValidator<AddPlanFeatureRequest>
{
    public AddPlanFeatureRequestValidator()
    {
        RuleFor(x => x.FeatureId).NotEmpty();
        RuleFor(x => x.LimitValue).GreaterThanOrEqualTo(0).When(x => x.LimitValue.HasValue);
        RuleFor(x => x.MeteredIncludedUnits).GreaterThanOrEqualTo(0).When(x => x.MeteredIncludedUnits.HasValue);
    }
}

public sealed class CreateAddonRequestValidator : AbstractValidator<CreateAddonRequest>
{
    public CreateAddonRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().Must(CatalogKey.IsValid)
            .WithMessage("Key must be 2–64 chars, alphanumeric or - _ .");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateAddonRequestValidator : AbstractValidator<UpdateAddonRequest>
{
    public UpdateAddonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class CreateBundleRequestValidator : AbstractValidator<CreateBundleRequest>
{
    public CreateBundleRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().Must(CatalogKey.IsValid)
            .WithMessage("Key must be 2–64 chars, alphanumeric or - _ .");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateBundleRequestValidator : AbstractValidator<UpdateBundleRequest>
{
    public UpdateBundleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class AddBundleItemRequestValidator : AbstractValidator<AddBundleItemRequest>
{
    public AddBundleItemRequestValidator()
    {
        RuleFor(x => x).Must(x =>
        {
            var n = (x.ProductId.HasValue ? 1 : 0)
                  + (x.PlanId.HasValue ? 1 : 0)
                  + (x.AddonId.HasValue ? 1 : 0);
            return n == 1;
        }).WithMessage("BundleItem must reference exactly one of ProductId, PlanId, or AddonId.");
    }
}

public sealed class CreatePriceRequestValidator : AbstractValidator<CreatePriceRequest>
{
    public CreatePriceRequestValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(c => c is { Length: 3 } && c.All(char.IsLetter) && c == c.ToUpperInvariant())
            .WithMessage("Currency must be a 3-letter ISO code in uppercase, e.g. USD.");
        RuleFor(x => x.AmountMinor).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BillingInterval).IsInEnum();
        RuleFor(x => x).Must(x =>
        {
            var n = (x.PlanId.HasValue ? 1 : 0)
                  + (x.AddonId.HasValue ? 1 : 0)
                  + (x.BundleId.HasValue ? 1 : 0);
            return n == 1;
        }).WithMessage("Price must reference exactly one of PlanId, AddonId, or BundleId.");
        RuleFor(x => x).Must(x => !x.EffectiveToUtc.HasValue || x.EffectiveToUtc.Value > x.EffectiveFromUtc)
            .WithMessage("EffectiveToUtc must be greater than EffectiveFromUtc.");
    }
}

public sealed class UpdatePriceRequestValidator : AbstractValidator<UpdatePriceRequest>
{
    public UpdatePriceRequestValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(c => c is { Length: 3 } && c.All(char.IsLetter) && c == c.ToUpperInvariant())
            .WithMessage("Currency must be a 3-letter ISO code in uppercase, e.g. USD.");
        RuleFor(x => x.AmountMinor).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BillingInterval).IsInEnum();
        RuleFor(x => x).Must(x => !x.EffectiveToUtc.HasValue || x.EffectiveToUtc.Value > x.EffectiveFromUtc)
            .WithMessage("EffectiveToUtc must be greater than EffectiveFromUtc.");
    }
}
