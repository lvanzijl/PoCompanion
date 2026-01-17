using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests to verify that EF Core concurrency gate prevents concurrent DbContext access.
/// These tests simulate the real-world scenario where multiple parallel tasks
/// access TfsConfigurationService (which uses DbContext) simultaneously.
/// </summary>
[TestClass]
public class EfConcurrencyGateTests
{
    /// <summary>
    /// Verifies that TfsConfigurationService can handle concurrent GetConfigEntityAsync calls
    /// without throwing "A second operation was started on this context" exceptions.
    /// 
    /// This simulates the real scenario where RealTfsClient makes parallel PR detail fetches,
    /// each calling GetConfigEntityAsync internally.
    /// </summary>
    [TestMethod]
    public async Task TfsConfigurationService_ConcurrentGetConfigEntityAsync_DoesNotThrowConcurrencyException()
    {
        // Arrange: Create in-memory database and services
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"ConcurrencyTest_{Guid.NewGuid()}")
            .Options;

        await using var context = new PoToolDbContext(options);
        
        // Seed a config
        context.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = "https://test.com",
            Project = "TestProject",
            DefaultAreaPath = "Test\\Path",
            ApiVersion = "7.0",
            TimeoutSeconds = 30,
            UseDefaultCredentials = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        // Create service with gate
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var gateLogger = loggerFactory.CreateLogger<EfConcurrencyGate>();
        var serviceLogger = loggerFactory.CreateLogger<TfsConfigurationService>();
        
        var gate = new EfConcurrencyGate(gateLogger);
        var service = new TfsConfigurationService(context, serviceLogger, gate);

        // Act: Simulate 10 parallel calls (simulates parallel PR detail fetches)
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.GetConfigEntityAsync(CancellationToken.None))
            .ToList();

        // Assert: All tasks should complete without exception
        var results = await Task.WhenAll(tasks);
        
        Assert.HasCount(10, results, "Should have 10 results");
        Assert.IsTrue(results.All(r => r != null), "All results should be non-null");
        Assert.IsTrue(results.All(r => r!.Project == "TestProject"), "All results should have correct project");
    }

    /// <summary>
    /// Verifies that the EF concurrency gate serializes operations
    /// (i.e., they don't all execute simultaneously).
    /// </summary>
    [TestMethod]
    public async Task EfConcurrencyGate_SerializesOperations()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var gateLogger = loggerFactory.CreateLogger<EfConcurrencyGate>();
        var gate = new EfConcurrencyGate(gateLogger);

        var concurrentExecutions = 0;
        var maxConcurrentExecutions = 0;
        var lockObj = new object();

        // Act: Create 5 tasks that track concurrent execution
        var tasks = Enumerable.Range(0, 5)
            .Select(async _ =>
            {
                return await gate.ExecuteAsync(async () =>
                {
                    lock (lockObj)
                    {
                        concurrentExecutions++;
                        if (concurrentExecutions > maxConcurrentExecutions)
                        {
                            maxConcurrentExecutions = concurrentExecutions;
                        }
                    }

                    // Simulate some work
                    await Task.Delay(50);

                    lock (lockObj)
                    {
                        concurrentExecutions--;
                    }

                    return true;
                });
            })
            .ToList();

        await Task.WhenAll(tasks);

        // Assert: Max concurrent executions should be 1 (serialized)
        Assert.AreEqual(1, maxConcurrentExecutions, 
            "Gate should serialize operations - only 1 should execute at a time");
    }

    /// <summary>
    /// Verifies that the gate can be disposed safely.
    /// </summary>
    [TestMethod]
    public void EfConcurrencyGate_Dispose_DoesNotThrow()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var gateLogger = loggerFactory.CreateLogger<EfConcurrencyGate>();
        var gate = new EfConcurrencyGate(gateLogger);

        // Act & Assert: Dispose should not throw
        gate.Dispose();
        
        // Second dispose should also not throw (idempotent)
        gate.Dispose();
    }

    /// <summary>
    /// Verifies that using a disposed gate throws ObjectDisposedException.
    /// </summary>
    [TestMethod]
    public async Task EfConcurrencyGate_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var gateLogger = loggerFactory.CreateLogger<EfConcurrencyGate>();
        var gate = new EfConcurrencyGate(gateLogger);
        gate.Dispose();

        // Act & Assert
        try
        {
            await gate.ExecuteAsync(async () =>
            {
                await Task.CompletedTask;
                return true;
            });
            Assert.Fail("Expected ObjectDisposedException");
        }
        catch (ObjectDisposedException)
        {
            // Expected
        }
    }
}
