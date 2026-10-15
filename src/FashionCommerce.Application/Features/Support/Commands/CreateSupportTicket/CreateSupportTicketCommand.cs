using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Support;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FashionCommerce.Application.Features.Support.Commands.CreateSupportTicket;

public record CreateSupportTicketCommand : IRequest<Result<Guid>>, IAuditableRequest
{
    public Guid RequesterId { get; init; }
    public string RequesterRole { get; init; } = default!;

    public string Category { get; init; } = default!;
    public string? Priority { get; init; }
    public string Subject { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string? ClientContextJson { get; init; } // app version, device, browser — attached automatically by the client for bug reports
    public List<string> AttachmentUrls { get; init; } = new();

    public string Action => "SupportTicket.Created";
    public string EntityName => "SupportTicket";
}

public class CreateSupportTicketCommandValidator : AbstractValidator<CreateSupportTicketCommand>
{
    public CreateSupportTicketCommandValidator()
    {
        RuleFor(x => x.Category).Must(c => Enum.TryParse<SupportTicketCategory>(c, out _)).WithMessage("Invalid category.");
        RuleFor(x => x.Priority).Must(p => Enum.TryParse<SupportTicketPriority>(p, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Priority)).WithMessage("Invalid priority.");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
    }
}

public class CreateSupportTicketCommandHandler : IRequestHandler<CreateSupportTicketCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateSupportTicketCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = new SupportTicket
        {
            TicketNumber = GenerateTicketNumber(),
            RequesterId = request.RequesterId,
            RequesterRole = Enum.Parse<UserRole>(request.RequesterRole),
            Category = Enum.Parse<SupportTicketCategory>(request.Category),
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? SupportTicketPriority.Normal : Enum.Parse<SupportTicketPriority>(request.Priority),
            Subject = request.Subject,
            Description = request.Description,
            ClientContextJson = request.ClientContextJson
        };

        foreach (var url in request.AttachmentUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
            ticket.Attachments.Add(new SupportTicketAttachment { SupportTicketId = ticket.Id, Url = url, FileName = Path.GetFileName(url) });

        _context.SupportTickets.Add(ticket);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(ticket.Id);
    }

    private static string GenerateTicketNumber() => $"TCK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
