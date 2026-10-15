using FashionCommerce.Domain.Entities.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Support;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("SupportTickets");

        builder.Property(t => t.TicketNumber).IsRequired().HasMaxLength(30);
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(300);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(4000);
        builder.Property(t => t.ClientContextJson).HasColumnType("nvarchar(max)");
        builder.Property(t => t.ResolutionNote).HasMaxLength(2000);

        builder.HasIndex(t => t.TicketNumber).IsUnique();
        builder.HasIndex(t => t.RequesterId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssignedAgentId);

        builder.HasMany(t => t.Messages)
            .WithOne(m => m.SupportTicket)
            .HasForeignKey(m => m.SupportTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Attachments)
            .WithOne(a => a.SupportTicket)
            .HasForeignKey(a => a.SupportTicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> builder)
    {
        builder.ToTable("SupportTicketMessages");
        builder.Property(m => m.Content).IsRequired().HasMaxLength(4000);
    }
}

public class SupportTicketAttachmentConfiguration : IEntityTypeConfiguration<SupportTicketAttachment>
{
    public void Configure(EntityTypeBuilder<SupportTicketAttachment> builder)
    {
        builder.ToTable("SupportTicketAttachments");
        builder.Property(a => a.Url).IsRequired().HasMaxLength(2048);
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(300);
    }
}
