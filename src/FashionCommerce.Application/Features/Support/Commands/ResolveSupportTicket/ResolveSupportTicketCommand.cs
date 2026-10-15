using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Support;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Support.Commands.ResolveSupportTicket;

public record ResolveSupportTicketCommand : IRequest<Result>, IAuditableRequest
{
    public Guid SupportTicketId { get; init; }
    public string ResolutionNote { get; init; } = default!;

    public string Action => "SupportTicket.Resolved";
    public string EntityName => "SupportTicket";
}

public class ResolveSupportTicketCommandValidator : AbstractValidator<ResolveSupportTicketCommand>
{
    public ResolveSupportTicketCommandValidator()
    {
        RuleFor(x => x.SupportTicketId).NotEmpty();
        RuleFor(x => x.ResolutionNote).NotEmpty().MaximumLength(2000);
    }
}

public class ResolveSupportTicketCommandHandler : IRequestHandler<ResolveSupportTicketCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public ResolveSupportTicketCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ResolveSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == request.SupportTicketId, cancellationToken);
        if (ticket is null)
            throw new NotFoundException(nameof(SupportTicket), request.SupportTicketId);

        ticket.Resolve(request.ResolutionNote);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
