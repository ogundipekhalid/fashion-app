using FashionCommerce.Application.Features.Payouts.Commands.ReleaseEligibleFunds;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FashionCommerce.Infrastructure.Jobs;

/// <summary>
/// Thin scheduled wrapper around ReleaseEligibleFundsCommand — the command
/// itself was already correct (built in the Payouts module); this job is
/// what makes it actually run automatically instead of needing an Admin
/// to hit the manual endpoint.
/// </summary>
public class FundReleaseJob
{
    private readonly ISender _mediator;
    private readonly ILogger<FundReleaseJob> _logger;

    public FundReleaseJob(ISender mediator, ILogger<FundReleaseJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ReleaseEligibleFundsCommand(), cancellationToken);

        if (result.Succeeded)
            _logger.LogInformation("FundReleaseJob: released {Count} commission transaction(s) to Available balance.", result.Value);
        else
            _logger.LogWarning("FundReleaseJob failed: {Errors}", string.Join("; ", result.Errors));
    }
}
