using System.Reflection;
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
using FashionCommerce.Persistence.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Persistence.Context;

/// <summary>
/// The concrete EF Core DbContext. Extends IdentityDbContext so
/// AspNetUsers/AspNetRoles live in the same database as the rest of the
/// platform, and implements IApplicationDbContext so the Application layer
/// only ever depends on the interface.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationIdentityUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    private readonly IMediator? _mediator;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IMediator? mediator = null) : base(options)
    {
        _mediator = mediator;
    }

    // Identity
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<FavoriteVendor> FavoriteVendors => Set<FavoriteVendor>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Catalog
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

    // Wishlist / Cart
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    // Orders / Payments
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookLog> PaymentWebhookLogs => Set<PaymentWebhookLog>();

    // Payouts
    public DbSet<VendorWallet> VendorWallets => Set<VendorWallet>();
    public DbSet<CommissionTransaction> CommissionTransactions => Set<CommissionTransaction>();
    public DbSet<PayoutRequest> PayoutRequests => Set<PayoutRequest>();

    // Refunds
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();
    public DbSet<RefundEvidence> RefundEvidences => Set<RefundEvidence>();
    public DbSet<RefundAuditLog> RefundAuditLogs => Set<RefundAuditLog>();

    // Marketing
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();

    // Chat / Support
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<SupportTicketAttachment> SupportTicketAttachments => Set<SupportTicketAttachment>();
    public DbSet<BotConversation> BotConversations => Set<BotConversation>();
    public DbSet<BotMessage> BotMessages => Set<BotMessage>();

    // Notifications / Audit
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Platform configuration
    public DbSet<PlatformSettings> Settings => Set<PlatformSettings>();

    // Styling / Lookbook
    public DbSet<StylePost> StylePosts => Set<StylePost>();
    public DbSet<StylePostLike> StylePostLikes => Set<StylePostLike>();
    public DbSet<StylePostComment> StylePostComments => Set<StylePostComment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity's own configuration first

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Global soft-delete query filter: applied to every entity implementing
        // ISoftDelete so `IsDeleted = true` rows are invisible to normal queries
        // without every LINQ query needing `.Where(x => !x.IsDeleted)` by hand.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(BuildSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                var filter = method.Invoke(null, null);
                entityType.SetQueryFilter((System.Linq.Expressions.LambdaExpression)filter!);
            }
        }

        // Identity table renames for a cleaner schema (optional but conventional in this codebase).
        builder.Entity<ApplicationIdentityUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
    }

    private static System.Linq.Expressions.Expression<Func<TEntity, bool>> BuildSoftDeleteFilter<TEntity>()
        where TEntity : class, ISoftDelete
        => e => !e.IsDeleted;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch domain events raised on any BaseEntity in this SaveChanges
        // batch AFTER the transaction commits, so event handlers (e.g. "send
        // order confirmation email") only fire once data is durably persisted.
        if (_mediator is not null)
        {
            var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count != 0)
                .ToList();

            foreach (var entity in entitiesWithEvents)
            {
                var events = entity.DomainEvents.ToList();
                entity.ClearDomainEvents();

                foreach (var domainEvent in events)
                    await _mediator.Publish(domainEvent, cancellationToken);
            }
        }

        return result;
    }
}
