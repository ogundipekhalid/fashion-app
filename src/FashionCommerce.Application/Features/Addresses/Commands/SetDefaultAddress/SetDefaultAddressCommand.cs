using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Addresses.Commands.SetDefaultAddress;

public record SetDefaultAddressCommand(Guid AddressId, Guid CustomerId) : IRequest<Result>;

public class SetDefaultAddressCommandValidator : AbstractValidator<SetDefaultAddressCommand>
{
    public SetDefaultAddressCommandValidator()
    {
        RuleFor(x => x.AddressId).NotEmpty();
    }
}

public class SetDefaultAddressCommandHandler : IRequestHandler<SetDefaultAddressCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public SetDefaultAddressCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == request.AddressId, cancellationToken);
        if (address is null)
            throw new NotFoundException(nameof(Address), request.AddressId);

        if (address.CustomerId != request.CustomerId)
            throw new ForbiddenAccessException("This address does not belong to you.");

        var currentDefaults = await _context.Addresses
            .Where(a => a.CustomerId == request.CustomerId && a.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var addr in currentDefaults) addr.IsDefault = false;

        address.IsDefault = true;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
