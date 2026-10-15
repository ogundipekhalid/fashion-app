using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Audit;
using FashionCommerce.Domain.Entities.Cart;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Chat;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Marketing;
using FashionCommerce.Domain.Entities.Notifications;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Entities.Payments;
using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Entities.Refunds;
using FashionCommerce.Domain.Entities.Settings;
using FashionCommerce.Domain.Entities.Styling;
using FashionCommerce.Domain.Entities.Support;
using FashionCommerce.Domain.Entities.Wishlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FashionCommerce.Tests.Common;

/// <summary>
/// A plain DbContext (NOT IdentityDbContext — these tests never need
/// AspNetUsers/roles, only the business entities) implementing
/// IApplicationDbContext against EF Core's InMemory provider. Applies the
/// same IEntityTypeConfiguration classes from FashionCommerce.Persistence
/// via reflection, so relationships/keys match production exactly instead
/// of drifting from a hand-maintained duplicate model.
/// </summary>
public class TestApplicationDbContext : DbContext, IApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<FavoriteVendor> FavoriteVendors => Set<FavoriteVendor>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVideo> ProductVideos => Set<ProductVideo>();
    public DbSet<Product3DModel> Product3DModels => Set<Product3DModel>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();

    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookLog> PaymentWebhookLogs => Set<PaymentWebhookLog>();

    public DbSet<VendorWallet> VendorWallets => Set<VendorWallet>();
    public DbSet<CommissionTransaction> CommissionTransactions => Set<CommissionTransaction>();
    public DbSet<PayoutRequest> PayoutRequests => Set<PayoutRequest>();

    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();
    public DbSet<RefundEvidence> RefundEvidences => Set<RefundEvidence>();
    public DbSet<RefundAuditLog> RefundAuditLogs => Set<RefundAuditLog>();

    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<SupportTicketAttachment> SupportTicketAttachments => Set<SupportTicketAttachment>();
    public DbSet<BotConversation> BotConversations => Set<BotConversation>();
    public DbSet<BotMessage> BotMessages => Set<BotMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<PlatformSettings> Settings => Set<PlatformSettings>();

    public DbSet<StylePost> StylePosts => Set<StylePost>();
    public DbSet<StylePostLike> StylePostLikes => Set<StylePostLike>();
    public DbSet<StylePostComment> StylePostComments => Set<StylePostComment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Reuse the real entity configurations from Persistence rather than
        // hand-duplicating them — if a relationship/index/precision rule
        // changes in production, these tests automatically pick it up.
        builder.ApplyConfigurationsFromAssembly(typeof(Persistence.Context.ApplicationDbContext).Assembly);

        // SQL Server generates rowversion values, but the InMemory provider does not.
        // Keep the production mappings while making updates usable in these tests.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty(nameof(AuditableEntity.RowVersion));
            if (rowVersion is not null)
            {
                rowVersion.IsConcurrencyToken = false;
                rowVersion.ValueGenerated = ValueGenerated.Never;
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);
}
