using FashionCommerce.Application.Features.Payouts.Commands.ProcessPayout;
using FashionCommerce.Application.Features.Payouts.Commands.RejectPayout;
using FashionCommerce.Application.Features.Payouts.Commands.RequestPayout;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Payouts;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class PayoutFlowTests
{
    [Fact]
    public async Task Requesting_a_payout_immediately_debits_available_balance_into_withdrawn()
    {
        await using var context = TestDbContextFactory.Create();
        var (vendor, wallet) = await SeedVendorWithWalletAsync(context, availableBalance: 1000m);

        var handler = new RequestPayoutCommandHandler(context);
        var result = await handler.Handle(new RequestPayoutCommand { VendorId = vendor.Id, Amount = 600m }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var reloadedWallet = context.VendorWallets.Local.Single(w => w.VendorId == vendor.Id);
        reloadedWallet.AvailableBalance.Should().Be(400m);
        reloadedWallet.WithdrawnBalance.Should().Be(600m);

        var payoutRequest = context.PayoutRequests.Local.Should().ContainSingle().Subject;
        payoutRequest.Status.Should().Be(PayoutRequestStatus.Requested);
        payoutRequest.Amount.Should().Be(600m);
    }

    [Fact]
    public async Task Requesting_more_than_the_available_balance_fails_without_touching_the_wallet()
    {
        await using var context = TestDbContextFactory.Create();
        var (vendor, wallet) = await SeedVendorWithWalletAsync(context, availableBalance: 100m);

        var handler = new RequestPayoutCommandHandler(context);
        var result = await handler.Handle(new RequestPayoutCommand { VendorId = vendor.Id, Amount = 500m }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();

        var reloadedWallet = context.VendorWallets.Local.Single(w => w.VendorId == vendor.Id);
        reloadedWallet.AvailableBalance.Should().Be(100m); // untouched
        context.PayoutRequests.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task Requesting_without_bank_details_on_file_fails_with_a_clear_message()
    {
        await using var context = TestDbContextFactory.Create();
        var (vendor, _) = await SeedVendorWithWalletAsync(context, availableBalance: 1000m, hasBankDetails: false);

        var handler = new RequestPayoutCommandHandler(context);
        var result = await handler.Handle(new RequestPayoutCommand { VendorId = vendor.Id, Amount = 500m }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("bank account"));
    }

    [Fact]
    public async Task Processing_a_payout_marks_it_paid_and_records_the_transfer_reference()
    {
        await using var context = TestDbContextFactory.Create();
        var (vendor, wallet) = await SeedVendorWithWalletAsync(context, availableBalance: 1000m);
        var requestHandler = new RequestPayoutCommandHandler(context);
        await requestHandler.Handle(new RequestPayoutCommand { VendorId = vendor.Id, Amount = 500m }, CancellationToken.None);
        var payoutRequest = context.PayoutRequests.Local.Single();

        var adminId = Guid.NewGuid();
        var processHandler = new ProcessPayoutCommandHandler(context);
        var result = await processHandler.Handle(new ProcessPayoutCommand
        {
            PayoutRequestId = payoutRequest.Id,
            ExternalTransferReference = "TXN-EXTERNAL-123",
            ProcessedByAdminId = adminId
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        payoutRequest.Status.Should().Be(PayoutRequestStatus.Paid);
        payoutRequest.ExternalTransferReference.Should().Be("TXN-EXTERNAL-123");
        payoutRequest.ProcessedByAdminId.Should().Be(adminId);
    }

    [Fact]
    public async Task Rejecting_a_payout_reverses_the_original_debit_back_to_available()
    {
        await using var context = TestDbContextFactory.Create();
        var (vendor, wallet) = await SeedVendorWithWalletAsync(context, availableBalance: 1000m);
        var requestHandler = new RequestPayoutCommandHandler(context);
        await requestHandler.Handle(new RequestPayoutCommand { VendorId = vendor.Id, Amount = 500m }, CancellationToken.None);
        var payoutRequest = context.PayoutRequests.Local.Single();

        var rejectHandler = new RejectPayoutCommandHandler(context);
        var result = await rejectHandler.Handle(new RejectPayoutCommand
        {
            PayoutRequestId = payoutRequest.Id,
            Reason = "Bank account details could not be verified."
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        payoutRequest.Status.Should().Be(PayoutRequestStatus.Rejected);

        var reloadedWallet = context.VendorWallets.Local.Single(w => w.VendorId == vendor.Id);
        reloadedWallet.AvailableBalance.Should().Be(1000m); // fully reversed
        reloadedWallet.WithdrawnBalance.Should().Be(0m);
    }

    private static async Task<(Vendor Vendor, VendorWallet Wallet)> SeedVendorWithWalletAsync(
        TestApplicationDbContext context, decimal availableBalance, bool hasBankDetails = true)
    {
        var vendor = new Vendor
        {
            IdentityUserId = Guid.NewGuid(),
            BusinessName = "Test Vendor",
            ContactEmail = "vendor@test.dev",
            BankName = hasBankDetails ? "Test Bank" : null,
            BankAccountNumber = hasBankDetails ? "0123456789" : null,
            BankAccountName = hasBankDetails ? "Test Vendor" : null
        };

        var wallet = new VendorWallet { VendorId = vendor.Id, Vendor = vendor, AvailableBalance = availableBalance };

        context.Vendors.Add(vendor);
        context.VendorWallets.Add(wallet);
        await context.SaveChangesAsync(CancellationToken.None);

        return (vendor, wallet);
    }
}
