namespace FashionCommerce.Domain.Enums;

public enum OrderStatus
{
    PendingPayment = 1,
    Paid = 2,
    Processing = 3,
    Shipped = 4,
    OutForDelivery = 5,
    Delivered = 6,
    Cancelled = 7,
    Failed = 8
}

/// <summary>
/// Status of a single vendor's line-items within an order. Since this is a
/// multi-vendor marketplace, one Order can span several Vendors, each with
/// their own fulfilment status independent of the others.
/// </summary>
public enum OrderItemStatus
{
    Pending = 1,
    Confirmed = 2,
    Processing = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    RefundRequested = 7,
    Refunded = 8
}

public enum PaymentStatus
{
    Pending = 1,
    Authorized = 2,
    Successful = 3,
    Failed = 4,
    Refunded = 5,
    PartiallyRefunded = 6,
    Cancelled = 7
}

public enum PaymentProvider
{
    Paystack = 1,
    Flutterwave = 2,
    Stripe = 3,
    Wallet = 4,
    BankTransfer = 5
}

public enum RefundType
{
    Full = 1,
    Partial = 2
}

public enum RefundStatus
{
    Requested = 1,
    UnderReview = 2,
    AwaitingVendorResponse = 3,
    ApprovedByVendor = 4,
    RejectedByVendor = 5,
    EscalatedToAdmin = 6,
    ApprovedByAdmin = 7,
    RejectedByAdmin = 8,
    Processing = 9,
    Refunded = 10,
    Closed = 11
}

/// <summary>Who initiated / is responsible for the current refund decision step.</summary>
public enum RefundDecisionMaker
{
    System = 1,
    Vendor = 2,
    Admin = 3
}

public enum CommissionTransactionStatus
{
    Pending = 1,
    Settled = 2,
    Reversed = 3
}

public enum PayoutRequestStatus
{
    Requested = 1,
    Approved = 2,
    Processing = 3,
    Paid = 4,
    Rejected = 5,
    Failed = 6
}

public enum CouponType
{
    Percentage = 1,
    FixedAmount = 2,
    FreeShipping = 3
}
