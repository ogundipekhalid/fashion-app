using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Payouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Identity;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");

        builder.Property(v => v.BusinessName).IsRequired().HasMaxLength(200);
        builder.Property(v => v.BusinessDescription).HasMaxLength(2000);
        builder.Property(v => v.ContactEmail).IsRequired().HasMaxLength(256);
        builder.Property(v => v.ContactPhone).HasMaxLength(20);
        builder.Property(v => v.CommissionRatePercent).HasPrecision(5, 2);
        builder.Property(v => v.BankAccountNumber).HasMaxLength(50);
        builder.Property(v => v.BankAccountName).HasMaxLength(200);
        builder.Property(v => v.BankName).HasMaxLength(100);

        builder.HasIndex(v => v.IdentityUserId).IsUnique();
        builder.HasIndex(v => v.ContactEmail).IsUnique();

        builder.Property(v => v.RowVersion).IsRowVersion();

        builder.HasOne(v => v.Wallet)
            .WithOne(w => w.Vendor)
            .HasForeignKey<VendorWallet>(w => w.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Products)
            .WithOne(p => p.Vendor)
            .HasForeignKey(p => p.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.PayoutRequests)
            .WithOne(p => p.Vendor)
            .HasForeignKey(p => p.VendorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        builder.ToTable("Admins");

        builder.Property(a => a.FullName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Email).IsRequired().HasMaxLength(256);

        builder.HasIndex(a => a.IdentityUserId).IsUnique();
        builder.HasIndex(a => a.Email).IsUnique();

        builder.HasMany(a => a.Permissions)
            .WithOne(p => p.Admin)
            .HasForeignKey(p => p.AdminId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdminPermissionConfiguration : IEntityTypeConfiguration<AdminPermission>
{
    public void Configure(EntityTypeBuilder<AdminPermission> builder)
    {
        builder.ToTable("AdminPermissions");
        builder.Property(p => p.PermissionKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(p => new { p.AdminId, p.PermissionKey }).IsUnique();
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.Property(t => t.Token).IsRequired().HasMaxLength(500);
        builder.Property(t => t.JwtId).IsRequired().HasMaxLength(100);
        builder.Property(t => t.DeviceInfo).HasMaxLength(500);
        builder.Property(t => t.CreatedByIp).HasMaxLength(64);
        builder.Property(t => t.RevokedByIp).HasMaxLength(64);
        builder.Property(t => t.ReasonRevoked).HasMaxLength(300);

        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => t.IdentityUserId);
    }
}
