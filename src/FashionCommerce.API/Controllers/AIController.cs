using Asp.Versioning;
using FashionCommerce.Application.Features.AI.Commands.GenerateProductContent;
using FashionCommerce.Application.Features.AI.Commands.SummarizeReview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

/// <summary>
/// AI-assisted content generation. Everything here produces a suggestion
/// for a human to review — nothing in this controller writes directly to
/// a Product or publishes anything on its own.
/// </summary>
[ApiVersion("1.0")]
[Authorize]
public class AIController : ApiControllerBase
{
    /// <summary>Drafts a product description, SEO metadata, and tags from basic facts. Vendor reviews and edits before including it in CreateProduct/UpdateProduct.</summary>
    [HttpPost("product-content")]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> GenerateProductContent(GenerateProductContentCommand command) =>
        FromResult(await Mediator.Send(command));

    /// <summary>Generates a one-line AI summary for a review (populates ProductReview.AiSummary).</summary>
    [HttpPost("reviews/{id:guid}/summarize")]
    [Authorize(Roles = "Admin,SuperAdmin,Vendor")]
    public async Task<IActionResult> SummarizeReview(Guid id) =>
        FromResult(await Mediator.Send(new SummarizeReviewCommand(id)));
}
