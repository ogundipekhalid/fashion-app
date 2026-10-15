using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Addresses.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Addresses.Queries.GetMyAddresses;

public record GetMyAddressesQuery(Guid CustomerId) : IRequest<List<AddressDto>>;

public class GetMyAddressesQueryHandler : IRequestHandler<GetMyAddressesQuery, List<AddressDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyAddressesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AddressDto>> Handle(GetMyAddressesQuery request, CancellationToken cancellationToken) =>
        await _context.Addresses
            .Where(a => a.CustomerId == request.CustomerId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CreatedAt)
            .Select(a => new AddressDto(
                a.Id, a.Label, a.RecipientName, a.RecipientPhone,
                a.Street, a.City, a.State, a.Country, a.PostalCode,
                a.Type.ToString(), a.IsDefault))
            .ToListAsync(cancellationToken);
}
