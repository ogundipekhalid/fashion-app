namespace FashionCommerce.Application.Features.Addresses.DTOs;

public record AddressDto(
    Guid Id, string Label, string RecipientName, string RecipientPhone,
    string Street, string City, string State, string Country, string? PostalCode,
    string Type, bool IsDefault);
