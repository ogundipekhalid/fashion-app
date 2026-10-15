namespace FashionCommerce.Domain.Exceptions;

/// <summary>
/// Base type for all exceptions raised because a Domain invariant was
/// violated. The Application layer's global exception middleware maps
/// these to 400/409 HTTP responses (never 500s), since they represent
/// expected business rule violations, not bugs.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public class DomainValidationException : DomainException
{
    public DomainValidationException(string message) : base(message) { }
}

public class InsufficientStockException : DomainException
{
    public Guid ProductId { get; }
    public Guid ProductVariantId { get; }
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(Guid productId, Guid productVariantId, int requested, int available)
        : base($"Cannot reserve {requested} unit(s) of variant {productVariantId}: only {available} in stock.")
    {
        ProductId = productId;
        ProductVariantId = productVariantId;
        Requested = requested;
        Available = available;
    }
}

public class InvalidOrderStateTransitionException : DomainException
{
    public InvalidOrderStateTransitionException(string from, string to)
        : base($"Cannot transition order from status '{from}' to '{to}'.") { }
}

public class RefundNotEligibleException : DomainException
{
    public RefundNotEligibleException(string reason) : base(reason) { }
}

public class InvalidCouponException : DomainException
{
    public InvalidCouponException(string reason) : base(reason) { }
}
