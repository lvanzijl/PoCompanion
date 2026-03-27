using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class WorkItemBackfillStartProviderSqliteTests
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public async Task SetupAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<PoToolDbContext>(options => options.UseSqlite(_connection));
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [TestMethod]
    public async Task GetEarliestChangedDateUtcAsync_WithSqlite_UsesCreatedDateUtcAggregate()
    {
        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
            context.WorkItems.AddRange(
                new WorkItemEntity
                {
                    TfsId = 1001,
                    Type = "Feature",
                    Title = "Item 1",
                    AreaPath = "Area",
                    IterationPath = "Sprint",
                    State = "Active",
                    RetrievedAt = DateTimeOffset.UtcNow,
                    TfsChangedDate = DateTimeOffset.UtcNow,
                    TfsChangedDateUtc = DateTime.UtcNow,
                    CreatedDate = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
                    CreatedDateUtc = new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc)
                },
                new WorkItemEntity
                {
                    TfsId = 1002,
                    Type = "Feature",
                    Title = "Item 2",
                    AreaPath = "Area",
                    IterationPath = "Sprint",
                    State = "Active",
                    RetrievedAt = DateTimeOffset.UtcNow,
                    TfsChangedDate = DateTimeOffset.UtcNow,
                    TfsChangedDateUtc = DateTime.UtcNow,
                    CreatedDate = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
                    CreatedDateUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)
                },
                new WorkItemEntity
                {
                    TfsId = 1003,
                    Type = "Feature",
                    Title = "Item 3",
                    AreaPath = "Area",
                    IterationPath = "Sprint",
                    State = "Active",
                    RetrievedAt = DateTimeOffset.UtcNow,
                    TfsChangedDate = DateTimeOffset.UtcNow,
                    TfsChangedDateUtc = DateTime.UtcNow
                });
            await context.SaveChangesAsync();
        }

        var provider = new WorkItemBackfillStartProvider(_serviceProvider.GetRequiredService<IServiceScopeFactory>());

        var earliest = await provider.GetEarliestChangedDateUtcAsync([1001, 1002, 1003], CancellationToken.None);

        Assert.AreEqual(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero), earliest);
    }
}
