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

namespace FashionCommerce.Application.Common.Interfaces;

/// <summary>
/// The Application layer's view of the database — a pure abstraction with
/// no EF Core provider-specific config. FashionCommerce.Persistence's
/// ApplicationDbContext implements this. CQRS handlers depend on this
/// interface, never on the concrete DbContext, so they stay unit-testable
/// with an in-memory/fake implementation.
/// </summary>
public interface IApplicationDbContext
{
    // Identity
    DbSet<Customer> Customers { get; }
    DbSet<Vendor> Vendors { get; }
    DbSet<Admin> Admins { get; }
    DbSet<Address> Addresses { get; }
    DbSet<FavoriteVendor> FavoriteVendors { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    // Catalog
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Collection> Collections { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<ProductVideo> ProductVideos { get; }
    DbSet<Product3DModel> Product3DModels { get; }
    DbSet<ProductReview> ProductReviews { get; }
    DbSet<ProductTag> ProductTags { get; }

    // Wishlist / Cart
    DbSet<WishlistItem> WishlistItems { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }

    // Orders / Payments
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderStatusHistory> OrderStatusHistories { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentWebhookLog> PaymentWebhookLogs { get; }

    // Payouts
    DbSet<VendorWallet> VendorWallets { get; }
    DbSet<CommissionTransaction> CommissionTransactions { get; }
    DbSet<PayoutRequest> PayoutRequests { get; }

    // Refunds
    DbSet<RefundRequest> RefundRequests { get; }
    DbSet<RefundEvidence> RefundEvidences { get; }
    DbSet<RefundAuditLog> RefundAuditLogs { get; }

    // Marketing
    DbSet<Coupon> Coupons { get; }
    DbSet<CouponUsage> CouponUsages { get; }

    // Chat / Support
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<SupportTicket> SupportTickets { get; }
    DbSet<SupportTicketMessage> SupportTicketMessages { get; }
    DbSet<SupportTicketAttachment> SupportTicketAttachments { get; }
    DbSet<BotConversation> BotConversations { get; }
    DbSet<BotMessage> BotMessages { get; }

    // Notifications / Audit
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    // Platform configuration
    DbSet<PlatformSettings> Settings { get; }

    // Styling / Lookbook
    DbSet<StylePost> StylePosts { get; }
    DbSet<StylePostLike> StylePostLikes { get; }
    DbSet<StylePostComment> StylePostComments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
