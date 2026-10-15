using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Payouts.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Payouts.Queries.GetVendorWallet;

public record GetVendorWalletQuery(Guid VendorId) : IRequest<VendorWalletDto?>;

public class GetVendorWalletQueryHandler : IRequestHandler<GetVendorWalletQuery, VendorWalletDto?>
{
    private readonly IApplicationDbContext _context;

    public GetVendorWalletQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VendorWalletDto?> Handle(GetVendorWalletQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _context.VendorWallets.FirstOrDefaultAsync(w => w.VendorId == request.VendorId, cancellationToken);
        return wallet is null ? null : new VendorWalletDto(wallet.PendingBalance, wallet.AvailableBalance, wallet.WithdrawnBalance, wallet.RefundBalance, wallet.Currency);
    }
}
