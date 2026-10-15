using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Addresses.Commands.UpdateAddress;

public record UpdateAddressCommand : IRequest<Result>
{
    public Guid AddressId { get; init; }
    public Guid CustomerId { get; init; }
    public string Label { get; init; } = default!;
    public string RecipientName { get; init; } = default!;
    public string RecipientPhone { get; init; } = default!;
    public string Street { get; init; } = default!;
    public string City { get; init; } = default!;
    public string State { get; init; } = default!;
    public string Country { get; init; } = default!;
    public string? PostalCode { get; init; }
    public string Type { get; init; } = "Both";
}

public class UpdateAddressCommandValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressCommandValidator()
    {
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(50);
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RecipientPhone).NotEmpty().Matches(@"^\+?[0-9]{7,15}$");
        RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Type).Must(t => Enum.TryParse<AddressType>(t, out _)).WithMessage("Invalid address type.");
    }
}

public class UpdateAddressCommandHandler : IRequestHandler<UpdateAddressCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateAddressCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == request.AddressId, cancellationToken);
        if (address is null)
            throw new NotFoundException(nameof(Address), request.AddressId);

        if (address.CustomerId != request.CustomerId)
            throw new ForbiddenAccessException("This address does not belong to you.");

        address.Label = request.Label;
        address.RecipientName = request.RecipientName;
        address.RecipientPhone = request.RecipientPhone;
        address.Street = request.Street;
        address.City = request.City;
        address.State = request.State;
        address.Country = request.Country;
        address.PostalCode = request.PostalCode;
        address.Type = Enum.Parse<AddressType>(request.Type);

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
