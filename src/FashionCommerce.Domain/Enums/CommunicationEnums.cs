namespace FashionCommerce.Domain.Enums;

/// <summary>
/// What a Customer<->Vendor conversation is about. Every conversation is
/// scoped to a reason so vendors see context (which product / order / refund)
/// instead of an anonymous thread.
/// </summary>
public enum ConversationType
{
    ProductEnquiry = 1,
    OrderEnquiry = 2,
    RefundEnquiry = 3,
    GeneralVendorEnquiry = 4
}

public enum ConversationStatus
{
    Open = 1,
    AwaitingCustomerReply = 2,
    AwaitingVendorReply = 3,
    Resolved = 4,
    Closed = 5,
    Reported = 6,
    Blocked = 7
}

public enum MessageSenderType
{
    Customer = 1,
    Vendor = 2,
    Admin = 3,
    Bot = 4,
    System = 5
}

public enum MessageType
{
    Text = 1,
    Image = 2,
    Attachment = 3,
    ProductReference = 4,
    OrderReference = 5,
    System = 6
}

/// <summary>Category for the platform-wide "contact the developer / report an issue" bot & ticket system.</summary>
public enum SupportTicketCategory
{
    TechnicalIssue = 1,
    BugReport = 2,
    FeatureRequest = 3,
    BillingQuestion = 4,
    AccountIssue = 5,
    VendorOnboarding = 6,
    Other = 7
}

public enum SupportTicketPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

public enum SupportTicketStatus
{
    Open = 1,
    InProgress = 2,
    WaitingOnUser = 3,
    Resolved = 4,
    Closed = 5,
    Reopened = 6
}

/// <summary>Who/what is talking in a bot conversation turn.</summary>
public enum BotMessageSender
{
    User = 1,
    Bot = 2,
    HumanAgent = 3
}

/// <summary>Where the bot conversation is happening (lets one BotConversation entity serve both use cases).</summary>
public enum BotChannel
{
    AiStylingAssistant = 1,
    CustomerSupportBot = 2,
    DeveloperContactBot = 3
}

public enum NotificationType
{
    OrderPlaced = 1,
    OrderStatusChanged = 2,
    PaymentReceived = 3,
    PaymentFailed = 4,
    RefundStatusChanged = 5,
    NewChatMessage = 6,
    SupportTicketUpdated = 7,
    VendorPayoutProcessed = 8,
    ProductLowStock = 9,
    ProductApproved = 10,
    ProductRejected = 11,
    PromotionAlert = 12,
    AccountSecurity = 13
}

public enum NotificationChannel
{
    InApp = 1,
    Email = 2,
    Push = 3,
    Sms = 4
}
