using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Portfolio;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioEntryLookupTests
{
    [TestMethod]
    public void Build_ReturnsFirstResolvedProductEntryPerWorkItem()
    {
        var productId = 42;
        var events = new[]
        {
            CreateEvent(eventId: 2, workItemId: 100, updateId: 11, timestamp: DateTimeOffset.Parse("2026-03-01T09:00:00Z"), oldValue: null, newValue: "42"),
            CreateEvent(eventId: 3, workItemId: 100, updateId: 12, timestamp: DateTimeOffset.Parse("2026-03-04T09:00:00Z"), oldValue: "42", newValue: null),
            CreateEvent(eventId: 4, workItemId: 100, updateId: 13, timestamp: DateTimeOffset.Parse("2026-03-05T09:00:00Z"), oldValue: null, newValue: "42"),
            CreateEvent(eventId: 5, workItemId: 200, updateId: 14, timestamp: DateTimeOffset.Parse("2026-03-02T09:00:00Z"), oldValue: "7", newValue: "42"),
            CreateEvent(eventId: 6, workItemId: 200, updateId: 15, timestamp: DateTimeOffset.Parse("2026-03-03T09:00:00Z"), oldValue: "42", newValue: "42"),
            new FieldChangeEvent(7, 300, 16, "System.State", DateTimeOffset.Parse("2026-03-06T09:00:00Z"), DateTime.Parse("2026-03-06T09:00:00Z"), "New", "Active")
        };

        var result = PortfolioEntryLookup.Build(events, productId);

        Assert.HasCount(2, result);
        Assert.AreEqual(DateTimeOffset.Parse("2026-03-01T09:00:00Z"), result[100]);
        Assert.AreEqual(DateTimeOffset.Parse("2026-03-02T09:00:00Z"), result[200]);
    }

    [TestMethod]
    public void GetFirstEnteredPortfolioTimestamp_IgnoresNonEntryTransitions()
    {
        var events = new[]
        {
            CreateEvent(eventId: 1, workItemId: 100, updateId: 10, timestamp: DateTimeOffset.Parse("2026-03-01T09:00:00Z"), oldValue: "42", newValue: "42"),
            CreateEvent(eventId: 2, workItemId: 100, updateId: 11, timestamp: DateTimeOffset.Parse("2026-03-02T09:00:00Z"), oldValue: "42", newValue: null),
            CreateEvent(eventId: 3, workItemId: 100, updateId: 12, timestamp: DateTimeOffset.Parse("2026-03-03T09:00:00Z"), oldValue: "7", newValue: "8")
        };

        var result = PortfolioEntryLookup.GetFirstEnteredPortfolioTimestamp(events, productId: 42);

        Assert.IsNull(result);
    }

    private static FieldChangeEvent CreateEvent(
        int eventId,
        int workItemId,
        int updateId,
        DateTimeOffset timestamp,
        string? oldValue,
        string? newValue)
    {
        return new FieldChangeEvent(
            eventId,
            workItemId,
            updateId,
            PortfolioEntryLookup.ResolvedProductIdFieldRefName,
            timestamp,
            timestamp.UtcDateTime,
            oldValue,
            newValue);
    }
}
