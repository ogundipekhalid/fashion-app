using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Catalog.Commands.ApproveProduct;

public record ApproveProductCommand : IRequest<Result>, IAuditableRequest
{
    public Guid ProductId { get; init; }

    public string Action => "Product.Approved";
    public string EntityName => "Product";
}

public class ApproveProductCommandValidator : AbstractValidator<ApproveProductCommand>
{
    public ApproveProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public class ApproveProductCommandHandler : IRequestHandler<ApproveProductCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ApproveProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ApproveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (product is null)
            throw new NotFoundException(nameof(Domain.Entities.Catalog.Product), request.ProductId);

        product.Approve(); // raises ProductApprovedEvent, handled later by a Notification module handler to alert the vendor
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
