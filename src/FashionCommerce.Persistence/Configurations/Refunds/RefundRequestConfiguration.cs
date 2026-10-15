using FashionCommerce.Domain.Entities.Refunds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Refunds;

public class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        builder.ToTable("RefundRequests");

        builder.Property(r => r.Reason).IsRequired().HasMaxLength(1000);
        builder.Property(r => r.CustomerNote).HasMaxLength(1000);
        builder.Property(r => r.VendorResponseNote).HasMaxLength(1000);
        builder.Property(r => r.AdminDecisionNote).HasMaxLength(1000);

        builder.Property(r => r.RequestedAmount).HasPrecision(18, 2);
        builder.Property(r => r.ApprovedAmount).HasPrecision(18, 2);

        builder.HasIndex(r => r.OrderItemId);
        builder.HasIndex(r => r.CustomerId);
        builder.HasIndex(r => r.VendorId);
        builder.HasIndex(r => r.Status);

        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasMany(r => r.Evidence)
            .WithOne(e => e.RefundRequest)
            .HasForeignKey(e => e.RefundRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.AuditLogs)
            .WithOne(a => a.RefundRequest)
            .HasForeignKey(a => a.RefundRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefundEvidenceConfiguration : IEntityTypeConfiguration<RefundEvidence>
{
    public void Configure(EntityTypeBuilder<RefundEvidence> builder)
    {
        builder.ToTable("RefundEvidences");
        builder.Property(e => e.Url).IsRequired().HasMaxLength(2048);
        builder.Property(e => e.UploadedByRole).IsRequired().HasMaxLength(20);
    }
}

public class RefundAuditLogConfiguration : IEntityTypeConfiguration<RefundAuditLog>
{
    public void Configure(EntityTypeBuilder<RefundAuditLog> builder)
    {
        builder.ToTable("RefundAuditLogs");
        builder.Property(a => a.Message).IsRequired().HasMaxLength(1000);
        builder.HasIndex(a => a.RefundRequestId);
    }
}
