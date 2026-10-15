namespace FashionCommerce.Domain.Enums;

public enum UserRole
{
    Customer = 1,
    Vendor = 2,
    Admin = 3,
    SuperAdmin = 4,
    SupportAgent = 5,
    DeliveryAgent = 6
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Unisex = 3,
    PreferNotToSay = 4
}

public enum AddressType
{
    Shipping = 1,
    Billing = 2,
    Both = 3
}

public enum VendorVerificationStatus
{
    Pending = 1,
    UnderReview = 2,
    Verified = 3,
    Rejected = 4,
    Suspended = 5
}

public enum KycStatus
{
    NotSubmitted = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4
}

public enum AdminRole
{
    SuperAdmin = 1,
    Admin = 2,
    SupportAgent = 3
}

/// <summary>External identity providers for social login.</summary>
public enum ExternalAuthProvider
{
    Google = 1,
    Facebook = 2,
    Apple = 3
}
