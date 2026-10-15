using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Admin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Admin.Queries.GetCustomers;

public record GetCustomersQuery : IRequest<PaginatedList<CustomerAdminSummaryDto>>
{
    public string? SearchTerm { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, PaginatedList<CustomerAdminSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCustomersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<CustomerAdminSummaryDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            query = query.Where(c =>
                c.Email.Contains(request.SearchTerm) ||
                c.FirstName.Contains(request.SearchTerm) ||
                c.LastName.Contains(request.SearchTerm));

        var projected = query.OrderByDescending(c => c.CreatedAt).Select(c => new CustomerAdminSummaryDto(
            c.Id, c.FirstName + " " + c.LastName, c.Email, c.IsEmailVerified, c.Orders.Count, c.CreatedAt));

        return await PaginatedList<CustomerAdminSummaryDto>.CreateAsync(projected, request.PageNumber, request.PageSize);
    }
}
