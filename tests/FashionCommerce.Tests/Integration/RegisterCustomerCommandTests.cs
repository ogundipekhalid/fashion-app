using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Auth.Commands.RegisterCustomer;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class RegisterCustomerCommandTests
{
    private static Mock<IIdentityService> MockIdentityService(bool emailExists = false)
    {
        var identityService = new Mock<IIdentityService>();
        identityService.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(emailExists);
        identityService.Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), UserRole.Customer))
            .ReturnsAsync(Result<Guid>.Success(Guid.NewGuid()));
        identityService.Setup(s => s.GenerateEmailConfirmationTokenAsync(It.IsAny<Guid>()))
            .ReturnsAsync("fake-token");
        return identityService;
    }

    [Fact]
    public async Task Registering_a_new_customer_creates_the_profile_and_sends_a_verification_email()
    {
        await using var context = TestDbContextFactory.Create();
        var identityService = MockIdentityService();
        var emailService = new Mock<IEmailTemplateService>();

        var handler = new RegisterCustomerCommandHandler(context, identityService.Object, emailService.Object);

        var result = await handler.Handle(new RegisterCustomerCommand
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@test.dev",
            Password = "SuperSecure1!"
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var customer = context.Customers.Local.Should().ContainSingle().Subject;
        customer.FullName.Should().Be("Ada Lovelace");
        customer.IsEmailVerified.Should().BeFalse(); // not verified until the email link is clicked

        emailService.Verify(e => e.SendEmailVerificationAsync(
            "ada@test.dev", "Ada Lovelace", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Registering_with_an_email_that_already_exists_fails_without_creating_a_second_profile()
    {
        await using var context = TestDbContextFactory.Create();
        var identityService = MockIdentityService(emailExists: true);
        var emailService = new Mock<IEmailTemplateService>();

        var handler = new RegisterCustomerCommandHandler(context, identityService.Object, emailService.Object);

        var result = await handler.Handle(new RegisterCustomerCommand
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@test.dev",
            Password = "SuperSecure1!"
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        context.Customers.Local.Should().BeEmpty();
        emailService.Verify(e => e.SendEmailVerificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task If_Identity_user_creation_fails_no_Customer_profile_is_created()
    {
        await using var context = TestDbContextFactory.Create();
        var identityService = new Mock<IIdentityService>();
        identityService.Setup(s => s.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        identityService.Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), UserRole.Customer))
            .ReturnsAsync(Result<Guid>.Failure("Password does not meet complexity requirements."));

        var emailService = new Mock<IEmailTemplateService>();
        var handler = new RegisterCustomerCommandHandler(context, identityService.Object, emailService.Object);

        var result = await handler.Handle(new RegisterCustomerCommand
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@test.dev",
            Password = "weak"
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        context.Customers.Local.Should().BeEmpty();
    }
}
