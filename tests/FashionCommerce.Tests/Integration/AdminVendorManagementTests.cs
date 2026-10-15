using FashionCommerce.Application.Features.Admin.Commands.ToggleVendorActive;
using FashionCommerce.Application.Features.Admin.Commands.VerifyVendor;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class AdminVendorManagementTests
{
    [Fact]
    public async Task Approving_a_vendor_sets_status_to_Verified_and_notifies_them()
    {
        await using var context = TestDbContextFactory.Create();
        var vendor = await SeedPendingVendorAsync(context);
        var handler = new VerifyVendorCommandHandler(context);

        var result = await handler.Handle(new VerifyVendorCommand { VendorId = vendor.Id, Approved = true }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var reloadedVendor = context.Vendors.Local.Single(v => v.Id == vendor.Id);
        reloadedVendor.VerificationStatus.Should().Be(VendorVerificationStatus.Verified);
        reloadedVendor.RejectionReason.Should().BeNull();

        context.Notifications.Local.Should().ContainSingle(n => n.UserId == vendor.Id);
    }

    [Fact]
    public async Task Rejecting_a_vendor_requires_a_reason_and_records_it()
    {
        await using var context = TestDbContextFactory.Create();
        var vendor = await SeedPendingVendorAsync(context);
        var handler = new VerifyVendorCommandHandler(context);

        var result = await handler.Handle(new VerifyVendorCommand
        {
            VendorId = vendor.Id,
            Approved = false,
            RejectionReason = "Business registration document was unreadable."
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var reloadedVendor = context.Vendors.Local.Single(v => v.Id == vendor.Id);
        reloadedVendor.VerificationStatus.Should().Be(VendorVerificationStatus.Rejected);
        reloadedVendor.RejectionReason.Should().Be("Business registration document was unreadable.");
    }

    [Fact]
    public async Task Suspending_an_active_vendor_sets_IsActive_false_and_status_Suspended()
    {
        await using var context = TestDbContextFactory.Create();
        var vendor = await SeedVerifiedActiveVendorAsync(context);
        var handler = new ToggleVendorActiveCommandHandler(context);

        var result = await handler.Handle(new ToggleVendorActiveCommand
        {
            VendorId = vendor.Id,
            IsActive = false,
            Reason = "Repeated policy violations."
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var reloadedVendor = context.Vendors.Local.Single(v => v.Id == vendor.Id);
        reloadedVendor.IsActive.Should().BeFalse();
        reloadedVendor.VerificationStatus.Should().Be(VendorVerificationStatus.Suspended);
    }

    [Fact]
    public async Task Reactivating_a_suspended_vendor_restores_Verified_status()
    {
        await using var context = TestDbContextFactory.Create();
        var vendor = await SeedVerifiedActiveVendorAsync(context);
        var toggleHandler = new ToggleVendorActiveCommandHandler(context);
        await toggleHandler.Handle(new ToggleVendorActiveCommand { VendorId = vendor.Id, IsActive = false, Reason = "Under review." }, CancellationToken.None);

        var result = await toggleHandler.Handle(new ToggleVendorActiveCommand { VendorId = vendor.Id, IsActive = true }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var reloadedVendor = context.Vendors.Local.Single(v => v.Id == vendor.Id);
        reloadedVendor.IsActive.Should().BeTrue();
        reloadedVendor.VerificationStatus.Should().Be(VendorVerificationStatus.Verified); // restored, not left as Suspended
    }

    private static async Task<Vendor> SeedPendingVendorAsync(TestApplicationDbContext context)
    {
        var vendor = new Vendor
        {
            IdentityUserId = Guid.NewGuid(), BusinessName = "New Vendor", ContactEmail = "newvendor@test.dev",
            VerificationStatus = VendorVerificationStatus.Pending
        };
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync(CancellationToken.None);
        return vendor;
    }

    private static async Task<Vendor> SeedVerifiedActiveVendorAsync(TestApplicationDbContext context)
    {
        var vendor = new Vendor
        {
            IdentityUserId = Guid.NewGuid(), BusinessName = "Active Vendor", ContactEmail = "activevendor@test.dev",
            VerificationStatus = VendorVerificationStatus.Verified, IsActive = true
        };
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync(CancellationToken.None);
        return vendor;
    }
}
