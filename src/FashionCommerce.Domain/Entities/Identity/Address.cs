using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Identity;

public class Address : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public string Label { get; set; } = default!; // "Home", "Office"
    public string RecipientName { get; set; } = default!;
    public string RecipientPhone { get; set; } = default!;

    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string? PostalCode { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public AddressType Type { get; set; } = AddressType.Both;
    public bool IsDefault { get; set; }
}
