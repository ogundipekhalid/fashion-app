using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Addresses.Commands.DeleteAddress;

public record DeleteAddressCommand(Guid AddressId, Guid CustomerId) : IRequest<Result>;

public class DeleteAddressCommandValidator : AbstractValidator<DeleteAddressCommand>
{
    public DeleteAddressCommandValidator()
    {
        RuleFor(x => x.AddressId).NotEmpty();
    }
}

public class DeleteAddressCommandHandler : IRequestHandler<DeleteAddressCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public DeleteAddressCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(DeleteAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == request.AddressId, cancellationToken);
        if (address is null)
            throw new NotFoundException(nameof(Address), request.AddressId);

        if (address.CustomerId != request.CustomerId)
            throw new ForbiddenAccessException("This address does not belong to you.");

        var wasDefault = address.IsDefault;
        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync(cancellationToken);

        // Promote another address to default so the customer's checkout flow never has zero default addresses.
        if (wasDefault)
        {
            var nextAddress = await _context.Addresses
                .Where(a => a.CustomerId == request.CustomerId)
                .OrderBy(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextAddress is not null)
            {
                nextAddress.IsDefault = true;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return Result.Success();
    }
}
