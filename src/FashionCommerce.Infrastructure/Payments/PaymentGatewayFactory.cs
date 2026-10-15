using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Infrastructure.Payments;

public class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public PaymentGatewayFactory(IEnumerable<IPaymentGateway> gateways)
    {
        _gateways = gateways;
    }

    public IPaymentGateway Resolve(PaymentProvider provider)
    {
        var gateway = _gateways.FirstOrDefault(g => g.Provider == provider);
        if (gateway is null)
            throw new NotSupportedException(
                $"Payment provider '{provider}' is not yet implemented. Currently supported: {string.Join(", ", _gateways.Select(g => g.Provider))}.");

        return gateway;
    }
}
