using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Commands.RejectProduct;

public record RejectProductCommand : IRequest<Result>, IAuditableRequest
{
    public Guid ProductId { get; init; }
    public string Reason { get; init; } = default!;

    public string Action => "Product.Rejected";
    public string EntityName => "Product";
}

public class RejectProductCommandValidator : AbstractValidator<RejectProductCommand>
{
    public RejectProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public class RejectProductCommandHandler : IRequestHandler<RejectProductCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public RejectProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(RejectProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
            throw new NotFoundException(nameof(Domain.Entities.Catalog.Product), request.ProductId);

        product.Reject(request.Reason); // raises ProductRejectedEvent
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
