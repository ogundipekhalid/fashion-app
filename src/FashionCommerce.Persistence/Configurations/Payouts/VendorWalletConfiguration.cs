using FashionCommerce.Domain.Entities.Payouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Payouts;

public class VendorWalletConfiguration : IEntityTypeConfiguration<VendorWallet>
{
    public void Configure(EntityTypeBuilder<VendorWallet> builder)
    {
        builder.ToTable("VendorWallets");

        builder.Property(w => w.PendingBalance).HasPrecision(18, 2);
        builder.Property(w => w.AvailableBalance).HasPrecision(18, 2);
        builder.Property(w => w.WithdrawnBalance).HasPrecision(18, 2);
        builder.Property(w => w.RefundBalance).HasPrecision(18, 2);
        builder.Property(w => w.Currency).IsRequired().HasMaxLength(3);

        builder.HasIndex(w => w.VendorId).IsUnique();

        // Critical: every balance mutation goes through domain methods that
        // read-modify-write this row, so optimistic concurrency prevents two
        // concurrent payouts/refunds from silently overwriting each other.
        builder.Property(w => w.RowVersion).IsRowVersion();
    }
}

public class CommissionTransactionConfiguration : IEntityTypeConfiguration<CommissionTransaction>
{
    public void Configure(EntityTypeBuilder<CommissionTransaction> builder)
    {
        builder.ToTable("CommissionTransactions");

        builder.Property(t => t.GrossAmount).HasPrecision(18, 2);
        builder.Property(t => t.CommissionRatePercent).HasPrecision(5, 2);
        builder.Property(t => t.CommissionAmount).HasPrecision(18, 2);
        builder.Property(t => t.VendorEarning).HasPrecision(18, 2);

        builder.HasIndex(t => t.OrderItemId).IsUnique(); // exactly one ledger entry per order item
        builder.HasIndex(t => t.VendorId);

        builder.HasOne(t => t.Vendor)
            .WithMany()
            .HasForeignKey(t => t.VendorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PayoutRequestConfiguration : IEntityTypeConfiguration<PayoutRequest>
{
    public void Configure(EntityTypeBuilder<PayoutRequest> builder)
    {
        builder.ToTable("PayoutRequests");

        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.BankNameSnapshot).IsRequired().HasMaxLength(100);
        builder.Property(p => p.BankAccountNumberSnapshot).IsRequired().HasMaxLength(50);
        builder.Property(p => p.BankAccountNameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.ExternalTransferReference).HasMaxLength(200);

        builder.HasIndex(p => p.VendorId);
        builder.HasIndex(p => p.Status);
    }
}
