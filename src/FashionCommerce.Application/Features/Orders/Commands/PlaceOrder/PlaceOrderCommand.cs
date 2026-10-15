using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Admin.Queries.GetPlatformSettings;
using FashionCommerce.Application.Features.Orders.DTOs;
using FashionCommerce.Domain.Entities.Marketing;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Entities.Payments;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Orders.Commands.PlaceOrder;

public record PlaceOrderCommand : IRequest<Result<PlaceOrderResultDto>>, IAuditableRequest
{
    /// <summary>Resolved server-side from the JWT.</summary>
    public Guid CustomerId { get; init; }

    public Guid CartId { get; init; }
    public Guid ShippingAddressId { get; init; }
    public Guid BillingAddressId { get; init; }
    public string PaymentProvider { get; init; } = default!; // "Paystack" | "Flutterwave" | "Stripe"
    public string CallbackUrl { get; init; } = default!; // where the payment provider redirects after checkout

    public string Action => "Order.Placed";
    public string EntityName => "Order";
}

public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.ShippingAddressId).NotEmpty();
        RuleFor(x => x.BillingAddressId).NotEmpty();
        RuleFor(x => x.PaymentProvider).Must(p => Enum.TryParse<PaymentProvider>(p, out _)).WithMessage("Invalid payment provider.");
        RuleFor(x => x.CallbackUrl).NotEmpty();
    }
}

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Result<PlaceOrderResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;

    public PlaceOrderCommandHandler(IApplicationDbContext context, IPaymentGatewayFactory paymentGatewayFactory)
    {
        _context = context;
        _paymentGatewayFactory = paymentGatewayFactory;
    }

    public async Task<Result<PlaceOrderResultDto>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        // Tax rate and flat shipping fee now come from PlatformSettings
        // (Admin-configurable via UpdatePlatformSettingsCommand) rather than
        // being hardcoded — this is the connection that was previously
        // missing between the Analytics/Settings module and checkout.
        var platformSettings = await GetPlatformSettingsQueryHandler.GetOrCreateAsync(_context, cancellationToken);
        var taxRatePercent = platformSettings.DefaultTaxRatePercent;
        var flatShippingFee = platformSettings.DefaultShippingFee;

        var cart = await _context.Carts
            .Include(c => c.Items).ThenInclude(i => i.ProductVariant).ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.Id == request.CartId && c.CustomerId == request.CustomerId, cancellationToken);

        if (cart is null)
            throw new NotFoundException(nameof(Domain.Entities.Cart.Cart), request.CartId);

        if (cart.Items.Count == 0)
            return Result<PlaceOrderResultDto>.Failure("Your cart is empty.");

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (customer is null)
            return Result<PlaceOrderResultDto>.Failure("Customer not found.");

        var shippingAddressOk = await _context.Addresses.AnyAsync(a => a.Id == request.ShippingAddressId && a.CustomerId == request.CustomerId, cancellationToken);
        var billingAddressOk = await _context.Addresses.AnyAsync(a => a.Id == request.BillingAddressId && a.CustomerId == request.CustomerId, cancellationToken);
        if (!shippingAddressOk || !billingAddressOk)
            return Result<PlaceOrderResultDto>.Failure("Shipping or billing address does not belong to this customer.");

        // Re-validate stock at the moment of purchase — the cart is just a
        // wishlist-with-quantities, it never locks inventory.
        foreach (var item in cart.Items)
        {
            if (!item.ProductVariant.IsActive || item.ProductVariant.Product.Status != ProductStatus.Active)
                return Result<PlaceOrderResultDto>.Failure($"'{item.ProductVariant.Product.Name}' is no longer available.");

            if (item.ProductVariant.StockQuantity < item.Quantity)
                return Result<PlaceOrderResultDto>.Failure($"Only {item.ProductVariant.StockQuantity} unit(s) of '{item.ProductVariant.Product.Name}' ({item.ProductVariant.Color}/{item.ProductVariant.Size}) left in stock.");
        }

        var subtotal = cart.Subtotal; // uses each CartItem's PriceAtAdd snapshot — the authoritative total already shown to the customer at checkout

        decimal discount = 0;
        Coupon? coupon = null;
        if (!string.IsNullOrWhiteSpace(cart.AppliedCouponCode))
        {
            coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == cart.AppliedCouponCode, cancellationToken);
            if (coupon is not null && coupon.IsCurrentlyValid(DateTime.UtcNow))
            {
                try { discount = coupon.CalculateDiscount(subtotal); }
                catch (Domain.Exceptions.InvalidCouponException) { discount = 0; coupon = null; }
            }
            else
            {
                coupon = null;
            }
        }

        var taxAmount = Math.Round((subtotal - discount) * (taxRatePercent / 100m), 2);
        var total = subtotal - discount + taxAmount + flatShippingFee;

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            CustomerId = request.CustomerId,
            ShippingAddressId = request.ShippingAddressId,
            BillingAddressId = request.BillingAddressId,
            Subtotal = subtotal,
            DiscountAmount = discount,
            TaxAmount = taxAmount,
            ShippingFee = flatShippingFee,
            Total = total,
            AppliedCouponCode = coupon?.Code
        };

        foreach (var cartItem in cart.Items)
        {
            var variant = cartItem.ProductVariant;
            var product = variant.Product;

            variant.ReserveStock(cartItem.Quantity); // throws InsufficientStockException if a race condition slipped past the check above

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                VendorId = product.VendorId,
                ProductVariantId = variant.Id,
                ProductNameSnapshot = product.Name,
                ColorSnapshot = variant.Color,
                SizeSnapshot = variant.Size,
                ImageUrlSnapshot = variant.Images.FirstOrDefault()?.Url ?? product.Images.FirstOrDefault(i => i.IsPrimary)?.Url,
                Quantity = cartItem.Quantity,
                UnitPrice = cartItem.PriceAtAdd,
                CommissionRatePercent = 0 // set below, after we look up the vendor
            };

            order.Items.Add(orderItem);
        }

        // Commission rates require a Vendor lookup per distinct vendor in the order.
        var vendorIds = order.Items.Select(i => i.VendorId).Distinct().ToList();
        var vendorRates = await _context.Vendors
            .Where(v => vendorIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.CommissionRatePercent, cancellationToken);

        foreach (var orderItem in order.Items)
        {
            orderItem.CommissionRatePercent = vendorRates.GetValueOrDefault(orderItem.VendorId, 20.00m);
            orderItem.ComputeCommissionSplit();
        }

        _context.Orders.Add(order);

        // Initialize payment with the resolved provider. The Payment row is
        // created in Pending status; MarkPaymentSuccessful() and the vendor
        // wallet/commission crediting happen later, in the Payments module's
        // webhook handler — not here, since the customer hasn't actually
        // paid yet at this point in the flow.
        var provider = Enum.Parse<PaymentProvider>(request.PaymentProvider);
        var gateway = _paymentGatewayFactory.Resolve(provider);

        var initResult = await gateway.InitializeAsync(customer.Email, total, "NGN", order.OrderNumber, request.CallbackUrl, cancellationToken);
        if (!initResult.Succeeded)
            return Result<PlaceOrderResultDto>.Failure($"Could not initialize payment: {initResult.ErrorMessage}");

        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = provider,
            ProviderReference = initResult.ProviderReference!,
            Amount = total,
            Currency = "NGN",
            Status = Domain.Enums.PaymentStatus.Pending
        };
        _context.Payments.Add(payment);

        if (coupon is not null)
        {
            coupon.TimesUsed++;
            _context.CouponUsages.Add(new CouponUsage
            {
                CouponId = coupon.Id,
                CustomerId = request.CustomerId,
                OrderId = order.Id,
                DiscountApplied = discount
            });
        }

        // Cart is consumed on order placement, not on payment confirmation —
        // if payment fails, the customer starts a fresh cart rather than
        // silently reusing (now possibly stock-stale) items.
        _context.CartItems.RemoveRange(cart.Items);
        cart.AppliedCouponCode = null;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<PlaceOrderResultDto>.Success(
            new PlaceOrderResultDto(order.Id, order.OrderNumber, order.Total, initResult.AuthorizationUrl, payment.ProviderReference));
    }

    private static string GenerateOrderNumber() =>
        $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}
