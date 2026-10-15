using FashionCommerce.Application.Features.Addresses.Commands.CreateAddress;
using FashionCommerce.Application.Features.Addresses.Commands.DeleteAddress;
using FashionCommerce.Application.Features.Addresses.Commands.SetDefaultAddress;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class AddressCommandTests
{
    private static CreateAddressCommand BuildAddress(Guid customerId, string label, bool isDefault = false) => new()
    {
        CustomerId = customerId,
        Label = label,
        RecipientName = "Test Customer",
        RecipientPhone = "08000000000",
        Street = "1 Test St",
        City = "Lagos",
        State = "Lagos",
        Country = "Nigeria",
        Type = "Both",
        IsDefault = isDefault
    };

    [Fact]
    public async Task The_first_address_a_customer_adds_is_automatically_the_default()
    {
        await using var context = TestDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = new CreateAddressCommandHandler(context);

        var result = await handler.Handle(BuildAddress(customer.Id, "Home"), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var address = context.Addresses.Local.Should().ContainSingle().Subject;
        address.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Adding_a_second_address_as_default_unsets_the_previous_default()
    {
        await using var context = TestDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = new CreateAddressCommandHandler(context);

        await handler.Handle(BuildAddress(customer.Id, "Home"), CancellationToken.None);
        await handler.Handle(BuildAddress(customer.Id, "Office", isDefault: true), CancellationToken.None);

        var addresses = context.Addresses.Local.ToList();
        addresses.Should().HaveCount(2);
        addresses.Should().ContainSingle(a => a.IsDefault).Which.Label.Should().Be("Office");
    }

    [Fact]
    public async Task Deleting_the_default_address_promotes_the_next_oldest_address()
    {
        await using var context = TestDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var createHandler = new CreateAddressCommandHandler(context);

        var firstResult = await createHandler.Handle(BuildAddress(customer.Id, "Home"), CancellationToken.None);
        await createHandler.Handle(BuildAddress(customer.Id, "Office"), CancellationToken.None);

        var deleteHandler = new DeleteAddressCommandHandler(context);
        var deleteResult = await deleteHandler.Handle(new DeleteAddressCommand(firstResult.Value, customer.Id), CancellationToken.None);

        deleteResult.Succeeded.Should().BeTrue();
        var remaining = context.Addresses.Local.Should().ContainSingle().Subject;
        remaining.Label.Should().Be("Office");
        remaining.IsDefault.Should().BeTrue(); // promoted automatically — never zero default addresses
    }

    [Fact]
    public async Task SetDefaultAddress_switches_the_default_to_the_chosen_address()
    {
        await using var context = TestDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var createHandler = new CreateAddressCommandHandler(context);
        await createHandler.Handle(BuildAddress(customer.Id, "Home"), CancellationToken.None);
        var officeResult = await createHandler.Handle(BuildAddress(customer.Id, "Office"), CancellationToken.None);

        var setDefaultHandler = new SetDefaultAddressCommandHandler(context);
        var result = await setDefaultHandler.Handle(new SetDefaultAddressCommand(officeResult.Value, customer.Id), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var addresses = context.Addresses.Local.ToList();
        addresses.Should().ContainSingle(a => a.IsDefault).Which.Label.Should().Be("Office");
    }

    private static async Task<Customer> SeedCustomerAsync(TestApplicationDbContext context)
    {
        var customer = new Customer { IdentityUserId = Guid.NewGuid(), FirstName = "Test", LastName = "Customer", Email = "customer@test.dev" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync(CancellationToken.None);
        return customer;
    }
}
