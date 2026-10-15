using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Persistence.Context;
using FashionCommerce.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FashionCommerce.Persistence.Seed;

/// <summary>
/// Seeds the minimum needed to actually log in and explore the API after
/// a fresh `dotnet ef database update`: the six Identity roles, one
/// SuperAdmin account, and a handful of baseline categories/brands so
/// Catalog endpoints have something to return. Intended for local
/// development only — call from Program.cs behind an
/// `if (app.Environment.IsDevelopment())` check, never in Production.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var logger = provider.GetRequiredService<ILogger<ApplicationDbContext>>();
        var context = provider.GetRequiredService<ApplicationDbContext>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationIdentityUser>>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        await context.Database.MigrateAsync();

        await SeedRolesAsync(roleManager, logger);
        await SeedSuperAdminAsync(userManager, context, configuration, logger);
        await SeedCatalogBaselineAsync(context, logger);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager, ILogger logger)
    {
        foreach (var roleName in ApplicationRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName)) continue;

            await roleManager.CreateAsync(new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = roleName });
            logger.LogInformation("Seeded role: {Role}", roleName);
        }
    }

    private static async Task SeedSuperAdminAsync(
        UserManager<ApplicationIdentityUser> userManager, ApplicationDbContext context,
        IConfiguration configuration, ILogger logger)
    {
        var email = configuration["Seed:SuperAdminEmail"] ?? "superadmin@fashioncommerce.dev";
        var password = configuration["Seed:SuperAdminPassword"] ?? "ChangeMe123!";

        if (await userManager.FindByEmailAsync(email) is not null) return;

        var identityUser = new ApplicationIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true, // seeded account skips the email verification flow
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(identityUser, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to seed SuperAdmin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(identityUser, ApplicationRoles.SuperAdmin);

        context.Admins.Add(new Admin
        {
            IdentityUserId = identityUser.Id,
            FullName = "Super Admin",
            Email = email,
            Role = Domain.Enums.AdminRole.SuperAdmin,
            IsActive = true
        });

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded SuperAdmin account: {Email} — CHANGE THIS PASSWORD before deploying anywhere beyond local development.", email);
    }

    private static async Task SeedCatalogBaselineAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.Categories.AnyAsync()) return; // already seeded

        var categories = new[]
        {
            new Category { Name = "Men", Slug = "men", DisplayOrder = 1 },
            new Category { Name = "Women", Slug = "women", DisplayOrder = 2 },
            new Category { Name = "Kids", Slug = "kids", DisplayOrder = 3 },
            new Category { Name = "Accessories", Slug = "accessories", DisplayOrder = 4 },
            new Category { Name = "Footwear", Slug = "footwear", DisplayOrder = 5 }
        };
        context.Categories.AddRange(categories);

        var brands = new[]
        {
            new Brand { Name = "FashionCommerce Originals", Slug = "fashioncommerce-originals" },
            new Brand { Name = "Unbranded", Slug = "unbranded" }
        };
        context.Brands.AddRange(brands);

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {CategoryCount} categories and {BrandCount} brands.", categories.Length, brands.Length);
    }
}
