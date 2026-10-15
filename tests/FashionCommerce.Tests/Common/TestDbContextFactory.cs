using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Tests.Common;

public static class TestDbContextFactory
{
    /// <summary>
    /// A uniquely-named InMemory database per call, so tests never leak
    /// state into each other even when run in parallel — no shared fixture,
    /// no cleanup step required between tests.
    /// </summary>
    public static TestApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
