using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Addresses.Commands.CreateAddress;
using FashionCommerce.Application.Features.Addresses.Commands.DeleteAddress;
using FashionCommerce.Application.Features.Addresses.Commands.SetDefaultAddress;
using FashionCommerce.Application.Features.Addresses.Commands.UpdateAddress;
using FashionCommerce.Application.Features.Addresses.Queries.GetMyAddresses;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
[Authorize(Roles = "Customer")]
public class AddressesController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public AddressesController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyAddresses()
    {
        var addresses = await Mediator.Send(new GetMyAddressesQuery(RequireCustomerId()));
        return Ok(ApiResponse<object>.SuccessResponse(addresses));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAddressCommand command)
    {
        var result = await Mediator.Send(command with { CustomerId = RequireCustomerId() });
        return FromResult(result, "Address added.");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateAddressCommand command)
    {
        var result = await Mediator.Send(command with { AddressId = id, CustomerId = RequireCustomerId() });
        return FromResult(result, "Address updated.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await Mediator.Send(new DeleteAddressCommand(id, RequireCustomerId()));
        return FromResult(result, "Address deleted.");
    }

    [HttpPost("{id:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid id)
    {
        var result = await Mediator.Send(new SetDefaultAddressCommand(id, RequireCustomerId()));
        return FromResult(result, "Default address updated.");
    }

    private Guid RequireCustomerId() => _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
}
