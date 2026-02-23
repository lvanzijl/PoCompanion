using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class RealODataRevisionTfsClientTests
{
    [TestMethod]
    public void RevisionIngestionPaginationOptions_Defaults_UseLowerRiskQueryShape()
    {
        var options = new RevisionIngestionPaginationOptions();

        Assert.AreEqual(ODataRevisionSelectMode.Full, options.ODataSelectMode);
        Assert.IsTrue(options.ODataOrderByEnabled);
        Assert.AreEqual(ODataRevisionScopeMode.Range, options.ODataScopeMode);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_ParsesNextLinkAndRows()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 10, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" },
                { "WorkItemId": 10, "Revision": 2, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ],
              "@odata.nextLink": "https://analytics/page2"
            }
            """
        ]);

        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataSelectMode = ODataRevisionSelectMode.Minimal
        });
        var page = await client.GetRevisionsAsync(
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            scopedWorkItemIds: [10, 11]);

        Assert.HasCount(2, page.Revisions);
        Assert.IsNotNull(page.ContinuationToken);
        StringAssert.StartsWith(page.ContinuationToken, "next:");
        var firstRequest = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(firstRequest, "$orderby=ChangedDate asc,WorkItemId asc,Revision asc");
        StringAssert.Contains(firstRequest, "ChangedDate ge 2026-01-01T00:00:00.0000000Z");
        StringAssert.Contains(firstRequest, "WorkItemId ge 10 and WorkItemId le 11");
        StringAssert.Contains(firstRequest, "$select=WorkItemId,Revision,WorkItemType,Title,State,Reason,CreatedDate,ChangedDate,ClosedDate,Effort,BusinessValue,TagNames,Severity");
        StringAssert.Contains(firstRequest, "$expand=Iteration($select=IterationPath),Area($select=AreaPath),ChangedBy($select=UserName,UserEmail,UserId)");
        Assert.DoesNotContain(firstRequest, "IterationPath,");
        Assert.DoesNotContain(firstRequest, "AreaPath,");
        Assert.DoesNotContain(firstRequest, "ChangedBy,");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WithEndDateTime_AddsUpperBoundFilter()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": []
            }
            """
        ]);

        var client = CreateClient(handler);
        _ = await client.GetRevisionsAsync(
            startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            scopedWorkItemIds: [10, 11],
            endDateTime: DateTimeOffset.Parse("2026-02-01T00:00:00Z"));

        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(request, "ChangedDate ge 2026-01-01T00:00:00.0000000Z");
        StringAssert.Contains(request, "ChangedDate lt 2026-02-01T00:00:00.0000000Z");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_LogsEffectiveRequestFilterAndOrderBy()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var logger = new CapturingLogger<RealODataRevisionTfsClient>();
        var client = CreateClient(handler, logger: logger);

        _ = await client.GetRevisionsAsync(
            startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            scopedWorkItemIds: [10, 11]);

        var requestLog = logger.Messages
            .FirstOrDefault(message => message.Contains("REV_INGEST_ODATA_REQUEST", StringComparison.Ordinal));
        Assert.IsNotNull(requestLog);
        StringAssert.Contains(requestLog, "urlSource=InitialCanonical");
        StringAssert.Contains(requestLog, "hasFilter=True");
        StringAssert.Contains(requestLog, "hasOrderBy=True");
        StringAssert.Contains(requestLog, "effectiveFilter=ChangedDate ge 2026-01-01T00:00:00.0000000Z and WorkItemId ge 10 and WorkItemId le 11");
        StringAssert.Contains(requestLog, "effectiveOrderBy=ChangedDate asc,WorkItemId asc,Revision asc");
    }

    [TestMethod]
    public async Task GetWorkItemRevisionsAsync_FollowsPagedNextLink()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ],
              "@odata.nextLink": "https://analytics/page2"
            }
            """,
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 2, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Closed", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var revisions = await client.GetWorkItemRevisionsAsync(42);

        Assert.HasCount(2, revisions);
        var secondRequest = handler.RequestUris[1];
        Assert.AreEqual("https://analytics/page2", secondRequest);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenScopeHasDisjointRanges_RequestsOneSegmentPerRange()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 1, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """,
            """
            {
              "value": [
                { "WorkItemId": 10, "Revision": 1, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataScopeMode = ODataRevisionScopeMode.Range,
            ODataTop = 200
        });

        var first = await client.GetRevisionsAsync(scopedWorkItemIds: [1, 2, 3, 10, 11]);
        var second = await client.GetRevisionsAsync(
            continuationToken: first.ContinuationToken,
            scopedWorkItemIds: [1, 2, 3, 10, 11]);

        Assert.IsNotNull(first.ContinuationToken);
        Assert.IsNull(second.ContinuationToken);
        Assert.HasCount(2, handler.RequestUris);

        var firstRequest = Uri.UnescapeDataString(handler.RequestUris[0]);
        var secondRequest = Uri.UnescapeDataString(handler.RequestUris[1]);
        StringAssert.Contains(firstRequest, "WorkItemId ge 1 and WorkItemId le 3");
        StringAssert.Contains(secondRequest, "WorkItemId ge 10 and WorkItemId le 11");
        Assert.IsFalse(firstRequest.Contains("WorkItemId ge 1 and WorkItemId le 11", StringComparison.Ordinal));
        Assert.IsFalse(secondRequest.Contains("WorkItemId ge 1 and WorkItemId le 11", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenIteratingSegments_ProducesDeterministicNonDuplicatedCombinedStream()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 1, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """,
            """
            {
              "value": [
                { "WorkItemId": 10, "Revision": 1, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataScopeMode = ODataRevisionScopeMode.Range
        });

        var allRevisions = new List<WorkItemRevision>();
        string? token = null;
        do
        {
            var page = await client.GetRevisionsAsync(
                continuationToken: token,
                scopedWorkItemIds: [1, 2, 3, 10, 11]);
            allRevisions.AddRange(page.Revisions);
            token = page.ContinuationToken;
        }
        while (token is not null);

        Assert.HasCount(2, allRevisions);
        CollectionAssert.AreEqual(new[] { 1, 10 }, allRevisions.Select(revision => revision.WorkItemId).ToArray());
        Assert.AreEqual(
            2,
            allRevisions
                .Select(revision => (revision.WorkItemId, revision.RevisionNumber))
                .Distinct()
                .Count());
    }

    [TestMethod]
    public async Task GetRevisionsAsync_ParsesDotStyleFieldAliases()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "System.Id": 7, "System.Rev": 3, "System.ChangedDate": "2026-01-03T00:00:00Z", "System.Title": "Alias", "System.WorkItemType": "Task", "System.State": "Active", "System.IterationPath": "I", "System.AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var page = await client.GetRevisionsAsync();

        Assert.HasCount(1, page.Revisions);
        Assert.AreEqual(7, page.Revisions[0].WorkItemId);
        Assert.AreEqual(3, page.Revisions[0].RevisionNumber);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_MapsNavigationAndTagNamesFields()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                {
                  "WorkItemId": 17,
                  "Revision": 4,
                  "ChangedDate": "2026-01-03T00:00:00Z",
                  "WorkItemType": "Bug",
                  "Title": "Navigation",
                  "State": "Active",
                  "Iteration": { "IterationPath": "Team\\Sprint 1" },
                  "Area": { "AreaPath": "Team\\Area" },
                  "ChangedBy": { "UserName": "Ada", "UserEmail": "ada@example.com", "UserId": "1234" },
                  "BusinessValue": 9,
                  "TagNames": "alpha;beta"
                }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var page = await client.GetRevisionsAsync();

        Assert.HasCount(1, page.Revisions);
        var revision = page.Revisions[0];
        Assert.AreEqual("Team\\Sprint 1", revision.IterationPath);
        Assert.AreEqual("Team\\Area", revision.AreaPath);
        Assert.AreEqual("Ada", revision.ChangedBy);
        Assert.AreEqual(9, revision.BusinessValue);
        Assert.AreEqual("alpha;beta", revision.Tags);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_AllowsMissingIterationObject()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                {
                  "WorkItemId": 17,
                  "Revision": 4,
                  "ChangedDate": "2026-01-03T00:00:00Z",
                  "WorkItemType": "Bug",
                  "Title": "Missing Iteration",
                  "State": "Active"
                }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var page = await client.GetRevisionsAsync();

        Assert.HasCount(1, page.Revisions);
        Assert.AreEqual(string.Empty, page.Revisions[0].IterationPath);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_AllowsIterationObjectWithoutIterationPath()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                {
                  "WorkItemId": 69829,
                  "Revision": 1,
                  "ChangedDate": "2026-01-03T00:00:00Z",
                  "WorkItemType": "Bug",
                  "Title": "Missing IterationPath",
                  "State": "Active",
                  "Iteration": { "Name": "Sprint 1" }
                }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var page = await client.GetRevisionsAsync();

        Assert.HasCount(1, page.Revisions);
        Assert.AreEqual(69829, page.Revisions[0].WorkItemId);
        Assert.AreEqual(1, page.Revisions[0].RevisionNumber);
        Assert.AreEqual(string.Empty, page.Revisions[0].IterationPath);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_ThrowsWhenChangedDateMissing()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                {
                  "WorkItemId": 17,
                  "Revision": 4,
                  "WorkItemType": "Bug",
                  "Title": "Missing ChangedDate",
                  "State": "Active"
                }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var exception = await AssertThrowsAsync<InvalidOperationException>(() => client.GetRevisionsAsync());
        StringAssert.Contains(exception.Message, "ChangedDate");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_AllowsMissingMultipleOptionalFields()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                {
                  "WorkItemId": 18,
                  "Revision": 2,
                  "ChangedDate": "2026-01-03T00:00:00Z"
                }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var page = await client.GetRevisionsAsync();

        Assert.HasCount(1, page.Revisions);
        Assert.AreEqual(string.Empty, page.Revisions[0].Title);
        Assert.AreEqual(string.Empty, page.Revisions[0].IterationPath);
        Assert.AreEqual(string.Empty, page.Revisions[0].AreaPath);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_ThrowsWhenWorkItemIdMissing()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                {
                  "Revision": 4,
                  "ChangedDate": "2026-01-03T00:00:00Z",
                  "WorkItemType": "Bug",
                  "Title": "Missing WorkItemId",
                  "State": "Active"
                }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var exception = await AssertThrowsAsync<InvalidOperationException>(() => client.GetRevisionsAsync());

        StringAssert.Contains(exception.Message, "WorkItemId");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_SkipsRowsWhenRevisionMissing()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                {
                  "WorkItemId": 19,
                  "ChangedDate": "2026-01-03T00:00:00Z",
                  "WorkItemType": "Bug",
                  "Title": "Missing Revision",
                  "State": "Active"
                }
              ]
            }
            """
        ]);

        var client = CreateClient(handler);
        var page = await client.GetRevisionsAsync();

        Assert.HasCount(0, page.Revisions);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenScopeModeIsIdList_UsesIdListFilter()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 7, "Revision": 1, "ChangedDate": "2026-01-03T00:00:00Z", "Title": "Alias", "WorkItemType": "Task", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            MaxTotalPages = 10,
            MaxTotalRows = 100,
            MaxEmptyPages = 2,
            ODataScopeMode = ODataRevisionScopeMode.IdList,
            ODataMaxUrlLength = 4000
        });

        _ = await client.GetRevisionsAsync(scopedWorkItemIds: [7, 9, 11]);

        StringAssert.Contains(Uri.UnescapeDataString(handler.RequestUris[0]), "(WorkItemId eq 7 or WorkItemId eq 9 or WorkItemId eq 11)");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenQuotedDateLiteralsDisabled_UsesCompatibilityLiteral()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataQuoteDateStrings = false
        });

        _ = await client.GetRevisionsAsync(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(request, "ChangedDate ge 2026-01-01T00:00:00.0000000Z");
        Assert.IsFalse(request.Contains("datetimeoffset", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenQuotedDateLiteralsEnabled_UsesQuotedDateStringLiteral()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataQuoteDateStrings = true
        });

        _ = await client.GetRevisionsAsync(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(request, "ChangedDate ge '2026-01-01T00:00:00.0000000Z'");
        Assert.IsFalse(request.Contains("datetimeoffset", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenFullSelectAndOrderByDisabled_OmitsProjectionAndOrder()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataSelectMode = ODataRevisionSelectMode.Full,
            ODataOrderByEnabled = false
        });

        _ = await client.GetRevisionsAsync();

        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        Assert.IsFalse(request.Contains("$select=", StringComparison.Ordinal));
        Assert.IsFalse(request.Contains("$orderby=", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenNextLinkMissingAndPageIsFull_UsesSeekContinuationFallback()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" },
                { "WorkItemId": 42, "Revision": 2, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """,
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 3, "ChangedDate": "2026-01-03T00:00:00Z", "Title": "C", "WorkItemType": "Bug", "State": "Closed", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);

        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            MaxTotalPages = 10,
            MaxTotalRows = 100,
            MaxEmptyPages = 2,
            ODataTop = 2,
            ODataEnableSeekPagingFallback = true
        });

        var first = await client.GetRevisionsAsync(
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            scopedWorkItemIds: [42]);
        var second = await client.GetRevisionsAsync(
            continuationToken: first.ContinuationToken,
            scopedWorkItemIds: [42]);

        Assert.IsNotNull(first.ContinuationToken);
        StringAssert.StartsWith(first.ContinuationToken, "seek:");
        Assert.IsNull(second.ContinuationToken);
        var secondRequest = Uri.UnescapeDataString(handler.RequestUris[1]);
        StringAssert.Contains(secondRequest, "ChangedDate gt 2026-01-02T00:00:00.0000000Z");
        StringAssert.Contains(secondRequest, "WorkItemId eq 42 and Revision gt 2");
        Assert.IsFalse(secondRequest.Contains("datetimeoffset", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenNextLinkOmitsWindowFilter_UsesNextLinkVerbatim()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-10T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ],
              "@odata.nextLink": "https://analytics/WorkItemRevisions?$skiptoken=abc"
            }
            """,
            """{ "value": [] }"""
        ]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataTop = 50
        });

        var first = await client.GetRevisionsAsync(
            startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            scopedWorkItemIds: [42],
            endDateTime: DateTimeOffset.Parse("2026-02-01T00:00:00Z"));
        _ = await client.GetRevisionsAsync(
            startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            continuationToken: first.ContinuationToken,
            scopedWorkItemIds: [42],
            endDateTime: DateTimeOffset.Parse("2026-02-01T00:00:00Z"));

        var secondRequest = handler.RequestUris[1];
        Assert.AreEqual("https://analytics/WorkItemRevisions?$skiptoken=abc", secondRequest);
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenNextLinkContainsConflictingFilter_UsesNextLinkVerbatim()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-10T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ],
              "@odata.nextLink": "https://analytics/WorkItemRevisions?$filter=ChangedDate ge 2029-01-01T00:00:00Z&$orderby=ChangedDate desc&$skiptoken=xyz"
            }
            """,
            """{ "value": [] }"""
        ]);
        var client = CreateClient(handler);

        var first = await client.GetRevisionsAsync(
            startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            scopedWorkItemIds: [42],
            endDateTime: DateTimeOffset.Parse("2026-02-01T00:00:00Z"));
        _ = await client.GetRevisionsAsync(
            startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            continuationToken: first.ContinuationToken,
            scopedWorkItemIds: [42],
            endDateTime: DateTimeOffset.Parse("2026-02-01T00:00:00Z"));

        var secondRequest = Uri.UnescapeDataString(handler.RequestUris[1]);
        StringAssert.Contains(secondRequest, "ChangedDate ge 2029-01-01T00:00:00Z");
        StringAssert.Contains(secondRequest, "$orderby=ChangedDate desc");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenNextLinkTokenHasOuterWhitespace_UsesDecodedUrlVerbatim()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var logger = new CapturingLogger<RealODataRevisionTfsClient>();
        var client = CreateClient(handler, logger: logger);
        const string expectedUrl = "https://analytics/WorkItemRevisions?$skiptoken=whitespace";
        var encodedUrl = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(expectedUrl));
        var continuationToken = $"  next:{encodedUrl}|2|0|0|0|0|0  ";

        _ = await client.GetRevisionsAsync(continuationToken: continuationToken, scopedWorkItemIds: [42]);

        Assert.AreEqual(expectedUrl, handler.RequestUris[0]);
        var requestLog = logger.Messages
            .FirstOrDefault(message => message.Contains("REV_INGEST_ODATA_REQUEST", StringComparison.Ordinal));
        Assert.IsNotNull(requestLog);
        StringAssert.Contains(requestLog, "urlSource=NextLinkVerbatim");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenPageContainsOutOfWindowRevision_Throws()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-03-01T00:00:00Z", "Title": "Out", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);
        var client = CreateClient(handler);

        var exception = await AssertThrowsAsync<InvalidOperationException>(() =>
            client.GetRevisionsAsync(
                startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                scopedWorkItemIds: [42],
                endDateTime: DateTimeOffset.Parse("2026-02-01T00:00:00Z")));

        StringAssert.Contains(exception.Message, "outside requested window");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenEmptyNextLinkPage_ReSeeksFromLastCursor()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ],
              "@odata.nextLink": "https://analytics/WorkItemRevisions?$skiptoken=page2"
            }
            """,
            """
            {
              "value": [],
              "@odata.nextLink": "https://analytics/WorkItemRevisions?$skiptoken=page3"
            }
            """,
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 2, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataEnableSeekPagingFallback = true,
            ODataTop = 2,
            ODataSeekPageSize = 2
        });

        var first = await client.GetRevisionsAsync(
            startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            scopedWorkItemIds: [42],
            endDateTime: DateTimeOffset.Parse("2026-02-01T00:00:00Z"));
        var second = await client.GetRevisionsAsync(
            startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            continuationToken: first.ContinuationToken,
            scopedWorkItemIds: [42],
            endDateTime: DateTimeOffset.Parse("2026-02-01T00:00:00Z"));
        var third = await client.GetRevisionsAsync(
            startDateTime: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            continuationToken: second.ContinuationToken,
            scopedWorkItemIds: [42],
            endDateTime: DateTimeOffset.Parse("2026-02-01T00:00:00Z"));

        Assert.IsNotNull(second.ContinuationToken);
        StringAssert.StartsWith(second.ContinuationToken, "seek:");
        Assert.IsNull(third.ContinuationToken);
        Assert.HasCount(1, third.Revisions);
        var thirdRequest = Uri.UnescapeDataString(handler.RequestUris[2]);
        StringAssert.Contains(thirdRequest, "ChangedDate gt 2026-01-01T00:00:00.0000000Z");
        StringAssert.Contains(thirdRequest, "ChangedDate lt 2026-02-01T00:00:00.0000000Z");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenDateLiteralRejected_RetriesOnceWithAlternateQuoting()
    {
        var handler = new SequenceResponseMessageHandler(
        [
            (HttpStatusCode.BadRequest, """{ "error": { "message": "VS403483: Unrecognized 'Edm.String' literal 'ChangedDate'" } }"""),
            (HttpStatusCode.OK, """{ "value": [] }""")
        ]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataQuoteDateStrings = false
        });

        _ = await client.GetRevisionsAsync(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), scopedWorkItemIds: [42]);

        Assert.HasCount(2, handler.RequestUris);
        var firstRequest = Uri.UnescapeDataString(handler.RequestUris[0]);
        var secondRequest = Uri.UnescapeDataString(handler.RequestUris[1]);
        StringAssert.Contains(firstRequest, "ChangedDate ge 2026-01-01T00:00:00.0000000Z");
        StringAssert.Contains(secondRequest, "ChangedDate ge '2026-01-01T00:00:00.0000000Z'");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_SeekFallbackDetectsNoProgressAndThrows()
    {
        var repeatedPage = """
                           {
                             "value": [
                               { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" },
                               { "WorkItemId": 42, "Revision": 2, "ChangedDate": "2026-01-02T00:00:00Z", "Title": "B", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
                             ]
                           }
                           """;
        var handler = new QueueMessageHandler([repeatedPage, repeatedPage]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataTop = 2,
            ODataSeekPageSize = 2,
            ODataEnableSeekPagingFallback = true,
            MaxNoProgressPages = 1
        });

        var first = await client.GetRevisionsAsync(scopedWorkItemIds: [42]);
        var exception = await AssertThrowsAsync<InvalidOperationException>(() =>
            client.GetRevisionsAsync(continuationToken: first.ContinuationToken, scopedWorkItemIds: [42]));

        StringAssert.Contains(exception.Message, "no progress");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenSegmentedContinuationHasInnerToken_EncodesSegmentBounds()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 1, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ],
              "@odata.nextLink": "https://analytics/WorkItemRevisions?$skiptoken=seg1next"
            }
            """
        ]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataScopeMode = ODataRevisionScopeMode.Range
        });

        var page = await client.GetRevisionsAsync(scopedWorkItemIds: [1, 2, 10, 11]);

        Assert.IsNotNull(page.ContinuationToken);
        StringAssert.StartsWith(page.ContinuationToken, "seg:1:2|");
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenSegmentTokenBoundsMismatch_RestartsAtFirstSegmentWithoutInnerToken()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataScopeMode = ODataRevisionScopeMode.Range
        });
        var encodedInner = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("next:invalid"));
        var mismatchedToken = $"seg:999:1000|{encodedInner}";

        _ = await client.GetRevisionsAsync(continuationToken: mismatchedToken, scopedWorkItemIds: [1, 2, 10, 11]);

        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(request, "WorkItemId ge 1 and WorkItemId le 2");
        Assert.IsFalse(request.Contains("invalid", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenLegacySegmentTokenIndexProvided_ResolvesSegmentByIndex()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataScopeMode = ODataRevisionScopeMode.Range
        });
        var legacyToken = $"seg:1|{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(string.Empty))}";

        _ = await client.GetRevisionsAsync(continuationToken: legacyToken, scopedWorkItemIds: [1, 2, 10, 11]);

        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(request, "WorkItemId ge 10 and WorkItemId le 11");
        Assert.IsFalse(request.Contains("WorkItemId ge 1 and WorkItemId le 2", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenLegacySegmentTokenIndexOutOfRange_RestartsAtFirstSegmentWithoutInnerToken()
    {
        var handler = new QueueMessageHandler(["""{ "value": [] }"""]);
        var client = CreateClient(handler, new RevisionIngestionPaginationOptions
        {
            ODataScopeMode = ODataRevisionScopeMode.Range
        });
        var invalidLegacyToken = $"seg:99|{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("next:invalid"))}";

        _ = await client.GetRevisionsAsync(continuationToken: invalidLegacyToken, scopedWorkItemIds: [1, 2, 10, 11]);

        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(request, "WorkItemId ge 1 and WorkItemId le 2");
        Assert.IsFalse(request.Contains("invalid", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetWorkItemRevisionsAsync_UsesServerSideEqFilter()
    {
        var handler = new QueueMessageHandler(
        [
            """
            {
              "value": [
                { "WorkItemId": 42, "Revision": 1, "ChangedDate": "2026-01-01T00:00:00Z", "Title": "A", "WorkItemType": "Bug", "State": "Active", "IterationPath": "I", "AreaPath": "A" }
              ]
            }
            """
        ]);
        var client = CreateClient(handler);

        var revisions = await client.GetWorkItemRevisionsAsync(42);

        Assert.HasCount(1, revisions);
        var request = Uri.UnescapeDataString(handler.RequestUris[0]);
        StringAssert.Contains(request, "WorkItemId eq 42");
        Assert.IsFalse(request.Contains("WorkItemId ge 42 and WorkItemId le 42", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetRevisionsAsync_WhenRequestFails_ThrowsWithFilterAndResponseBody()
    {
        var handler = new StaticResponseMessageHandler(HttpStatusCode.BadRequest, """{ "error": { "message": "Invalid filter clause" } }""");
        var client = CreateClient(handler);

        var exception = await AssertThrowsAsync<HttpRequestException>(() =>
            client.GetRevisionsAsync(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), scopedWorkItemIds: [42]));

        StringAssert.Contains(exception.Message, "Filter=");
        StringAssert.Contains(exception.Message, "Invalid filter clause");
    }

    private static RealODataRevisionTfsClient CreateClient(
        HttpMessageHandler handler,
        RevisionIngestionPaginationOptions? options = null,
        ILogger<RealODataRevisionTfsClient>? logger = null)
    {
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(factory => factory.CreateClient("TfsClient.NTLM")).Returns(httpClient);

        var configService = new Mock<ITfsConfigurationService>();
        configService
            .Setup(service => service.GetConfigEntityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TfsConfigEntity
            {
                Url = "https://dev.azure.com/test",
                TimeoutSeconds = 30,
                AnalyticsODataBaseUrl = "https://analytics",
                AnalyticsODataEntitySetPath = "WorkItemRevisions"
            });

        var paginationOptions = new Mock<IOptionsMonitor<RevisionIngestionPaginationOptions>>();
        paginationOptions.Setup(current => current.CurrentValue).Returns(options ?? new RevisionIngestionPaginationOptions
        {
            MaxTotalPages = 10,
            MaxTotalRows = 100,
            MaxEmptyPages = 2
        });

        return new RealODataRevisionTfsClient(
            httpClientFactory.Object,
            configService.Object,
            new TfsRequestSender(NullLogger<TfsRequestSender>.Instance),
            paginationOptions.Object,
            logger ?? NullLogger<RealODataRevisionTfsClient>.Instance);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private sealed class NoopDisposable : IDisposable
        {
            public static NoopDisposable Instance { get; } = new();
            public void Dispose() { }
        }

        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NoopDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class QueueMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public QueueMessageHandler(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        public List<string> RequestUris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri?.ToString() ?? string.Empty);
            var payload = _responses.Count > 0 ? _responses.Dequeue() : "{ \"value\": [] }";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            });
        }
    }

    private sealed class StaticResponseMessageHandler(HttpStatusCode statusCode, string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(payload)
            });
        }
    }

    private sealed class SequenceResponseMessageHandler(IEnumerable<(HttpStatusCode StatusCode, string Payload)> responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Payload)> _responses = new(responses);

        public List<string> RequestUris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri?.ToString() ?? string.Empty);
            var next = _responses.Count > 0
                ? _responses.Dequeue()
                : (StatusCode: HttpStatusCode.OK, Payload: """{ "value": [] }""");
            return Task.FromResult(new HttpResponseMessage(next.StatusCode)
            {
                Content = new StringContent(next.Payload)
            });
        }
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
            throw new InvalidOperationException("Unreachable");
        }
        catch (TException ex)
        {
            return ex;
        }
    }
}
