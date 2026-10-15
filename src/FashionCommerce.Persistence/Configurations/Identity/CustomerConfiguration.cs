using FashionCommerce.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FashionCommerce.Persistence.Configurations.Identity;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.LastName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.PhoneNumber).HasMaxLength(20);
        builder.Property(c => c.ProfileImageUrl).HasMaxLength(2048);

        builder.HasIndex(c => c.IdentityUserId).IsUnique();
        builder.HasIndex(c => c.Email).IsUnique();

        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasMany(c => c.Addresses)
            .WithOne(a => a.Customer)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Orders)
            .WithOne(o => o.Customer)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict); // never cascade-delete order history

        builder.HasMany(c => c.WishlistItems)
            .WithOne(w => w.Customer)
            .HasForeignKey(w => w.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Reviews)
            .WithOne(r => r.Customer)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.Property(a => a.Label).IsRequired().HasMaxLength(50);
        builder.Property(a => a.RecipientName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.RecipientPhone).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Street).IsRequired().HasMaxLength(300);
        builder.Property(a => a.City).IsRequired().HasMaxLength(100);
        builder.Property(a => a.State).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Country).IsRequired().HasMaxLength(100);
        builder.Property(a => a.PostalCode).HasMaxLength(20);

        builder.HasIndex(a => a.CustomerId);
    }
}

public class FavoriteVendorConfiguration : IEntityTypeConfiguration<FavoriteVendor>
{
    public void Configure(EntityTypeBuilder<FavoriteVendor> builder)
    {
        builder.ToTable("FavoriteVendors");
        builder.HasIndex(f => new { f.CustomerId, f.VendorId }).IsUnique();

        builder.HasOne(f => f.Customer).WithMany(c => c.FavoriteVendors)
            .HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Vendor).WithMany(v => v.FavoritedByCustomers)
            .HasForeignKey(f => f.VendorId).OnDelete(DeleteBehavior.Cascade);
    }
}
