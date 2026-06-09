using Commerce.Application.Common.Exceptions;
using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Infrastructure.Payments;

public sealed class PaymentProviderRegistry : IPaymentProviderRegistry
{
    private readonly Dictionary<PaymentProviderType, IPaymentProvider> _providers;

    public PaymentProviderRegistry(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.ProviderType);
    }

    public IPaymentProvider Get(PaymentProviderType type)
    {
        if (!_providers.TryGetValue(type, out var p))
            throw new NotFoundException("PaymentProvider", type.ToString());
        return p;
    }

    public bool TryGet(PaymentProviderType type, out IPaymentProvider provider)
    {
        if (_providers.TryGetValue(type, out var p))
        {
            provider = p;
            return true;
        }
        provider = default!;
        return false;
    }
}
