using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Addresses.Commands.CreateAddress;

public record CreateAddressCommand : IRequest<Result<Guid>>
{
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
    public bool IsDefault { get; init; }
}

public class CreateAddressCommandValidator : AbstractValidator<CreateAddressCommand>
{
    public CreateAddressCommandValidator()
    {
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

public class CreateAddressCommandHandler : IRequestHandler<CreateAddressCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateAddressCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateAddressCommand request, CancellationToken cancellationToken)
    {
        var isFirstAddress = !await _context.Addresses.AnyAsync(a => a.CustomerId == request.CustomerId, cancellationToken);

        if (request.IsDefault || isFirstAddress)
        {
            var existingDefaults = await _context.Addresses
                .Where(a => a.CustomerId == request.CustomerId && a.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var addr in existingDefaults) addr.IsDefault = false;
        }

        var address = new Address
        {
            CustomerId = request.CustomerId,
            Label = request.Label,
            RecipientName = request.RecipientName,
            RecipientPhone = request.RecipientPhone,
            Street = request.Street,
            City = request.City,
            State = request.State,
            Country = request.Country,
            PostalCode = request.PostalCode,
            Type = Enum.Parse<AddressType>(request.Type),
            IsDefault = request.IsDefault || isFirstAddress
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(address.Id);
    }
}
