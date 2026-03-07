using System.Text.Json;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class RoadmapReportingServiceTests
{
    private RoadmapReportingService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new RoadmapReportingService();
    }

    #region GenerateReport

    [TestMethod]
    public void GenerateReport_EmptyLanes_ReturnsReportWithZeroCounts()
    {
        // Arrange
        var lanes = Array.Empty<RoadmapProductEntry>().ToList();

        // Act
        var report = _service.GenerateReport(lanes);

        // Assert
        Assert.AreEqual(0, report.TotalProducts);
        Assert.AreEqual(0, report.TotalEpics);
        Assert.IsEmpty(report.Products);
    }

    [TestMethod]
    public void GenerateReport_SingleProductWithEpics_ReturnCorrectCounts()
    {
        // Arrange
        var lanes = new List<RoadmapProductEntry>
        {
            new()
            {
                ProductName = "Product A",
                Epics = new List<RoadmapEpicEntry>
                {
                    new() { Order = 1, Title = "Epic 1", TfsId = 100 },
                    new() { Order = 2, Title = "Epic 2", TfsId = 200 }
                }
            }
        };

        // Act
        var report = _service.GenerateReport(lanes);

        // Assert
        Assert.AreEqual(1, report.TotalProducts);
        Assert.AreEqual(2, report.TotalEpics);
        Assert.AreEqual("Product A", report.Products[0].ProductName);
    }

    [TestMethod]
    public void GenerateReport_MultipleProducts_SumsEpicsAcrossProducts()
    {
        // Arrange
        var lanes = BuildSampleLanes();

        // Act
        var report = _service.GenerateReport(lanes);

        // Assert
        Assert.AreEqual(2, report.TotalProducts);
        Assert.AreEqual(3, report.TotalEpics);
    }

    [TestMethod]
    public void GenerateReport_ProductWithNoEpics_IncludedInReport()
    {
        // Arrange
        var lanes = new List<RoadmapProductEntry>
        {
            new() { ProductName = "Empty Product", Epics = [] }
        };

        // Act
        var report = _service.GenerateReport(lanes);

        // Assert
        Assert.AreEqual(1, report.TotalProducts);
        Assert.AreEqual(0, report.TotalEpics);
        Assert.AreEqual("Empty Product", report.Products[0].ProductName);
    }

    [TestMethod]
    public void GenerateReport_NullLanes_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _service.GenerateReport(null!));
    }

    #endregion

    #region ExportStructuredJson

    [TestMethod]
    public void ExportStructuredJson_EmptyLanes_ReturnsValidJson()
    {
        // Arrange
        var lanes = new List<RoadmapProductEntry>();

        // Act
        var json = _service.ExportStructuredJson(lanes);

        // Assert
        Assert.IsNotNull(json);
        var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("totalProducts").GetInt32());
        Assert.AreEqual(0, doc.RootElement.GetProperty("totalEpics").GetInt32());
    }

    [TestMethod]
    public void ExportStructuredJson_WithData_ContainsProductAndEpicData()
    {
        // Arrange
        var lanes = BuildSampleLanes();

        // Act
        var json = _service.ExportStructuredJson(lanes);

        // Assert
        var doc = JsonDocument.Parse(json);
        var products = doc.RootElement.GetProperty("products");
        Assert.AreEqual(2, products.GetArrayLength());
        Assert.AreEqual("Product A", products[0].GetProperty("productName").GetString());

        var epics = products[0].GetProperty("epics");
        Assert.AreEqual(2, epics.GetArrayLength());
        Assert.AreEqual(1, epics[0].GetProperty("order").GetInt32());
        Assert.AreEqual("Epic 1", epics[0].GetProperty("title").GetString());
        Assert.AreEqual(100, epics[0].GetProperty("tfsId").GetInt32());
    }

    [TestMethod]
    public void ExportStructuredJson_Deterministic_OrderPreserved()
    {
        // Arrange
        var lanes = BuildSampleLanes();

        // Act
        var json1 = _service.ExportStructuredJson(lanes);
        var json2 = _service.ExportStructuredJson(lanes);

        // Assert — product and epic order must remain stable
        var doc1 = JsonDocument.Parse(json1);
        var doc2 = JsonDocument.Parse(json2);

        var p1First = doc1.RootElement.GetProperty("products")[0].GetProperty("productName").GetString();
        var p2First = doc2.RootElement.GetProperty("products")[0].GetProperty("productName").GetString();
        Assert.AreEqual(p1First, p2First);
    }

    #endregion

    #region GenerateVisualRoadmap

    [TestMethod]
    public void GenerateVisualRoadmap_EmptyLanes_ContainsEmptyMessage()
    {
        // Arrange
        var lanes = new List<RoadmapProductEntry>();

        // Act
        var result = _service.GenerateVisualRoadmap(lanes);

        // Assert
        Assert.Contains("# Product Roadmap Overview", result);
        Assert.Contains("No products with roadmap epics found", result);
    }

    [TestMethod]
    public void GenerateVisualRoadmap_WithData_ContainsSummaryTable()
    {
        // Arrange
        var lanes = BuildSampleLanes();

        // Act
        var result = _service.GenerateVisualRoadmap(lanes);

        // Assert
        Assert.Contains("## Portfolio Summary", result);
        Assert.Contains("| # | Product | Roadmap Epics |", result);
        Assert.Contains("Product A", result);
        Assert.Contains("Product B", result);
    }

    [TestMethod]
    public void GenerateVisualRoadmap_WithData_ContainsEpicDetailsPerProduct()
    {
        // Arrange
        var lanes = BuildSampleLanes();

        // Act
        var result = _service.GenerateVisualRoadmap(lanes);

        // Assert
        Assert.Contains("## Product A", result);
        Assert.Contains("| #1 | Epic 1 | 100 |", result);
        Assert.Contains("| #2 | Epic 2 | 200 |", result);
        Assert.Contains("## Product B", result);
        Assert.Contains("| #1 | Epic 3 | 300 |", result);
    }

    [TestMethod]
    public void GenerateVisualRoadmap_ProductWithNoEpics_ShowsEmptyMessage()
    {
        // Arrange
        var lanes = new List<RoadmapProductEntry>
        {
            new() { ProductName = "Empty Product", Epics = [] }
        };

        // Act
        var result = _service.GenerateVisualRoadmap(lanes);

        // Assert
        Assert.Contains("## Empty Product", result);
        Assert.Contains("No roadmap epics", result);
    }

    [TestMethod]
    public void GenerateVisualRoadmap_EpicTitleWithPipe_EscapedInMarkdown()
    {
        // Arrange
        var lanes = new List<RoadmapProductEntry>
        {
            new()
            {
                ProductName = "Product X",
                Epics = new List<RoadmapEpicEntry>
                {
                    new() { Order = 1, Title = "Title | with pipe", TfsId = 999 }
                }
            }
        };

        // Act
        var result = _service.GenerateVisualRoadmap(lanes);

        // Assert
        Assert.Contains(@"Title \| with pipe", result);
    }

    [TestMethod]
    public void GenerateVisualRoadmap_NullLanes_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _service.GenerateVisualRoadmap(null!));
    }

    #endregion

    #region GenerateAiPrompt

    [TestMethod]
    public void GenerateAiPrompt_ExecutiveTemplate_ContainsRoadmapData()
    {
        // Arrange
        var lanes = BuildSampleLanes();

        // Act
        var prompt = _service.GenerateAiPrompt(lanes, RoadmapPromptTemplate.ExecutiveRoadmap);

        // Assert
        Assert.Contains("executive", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Product A", prompt);
        Assert.Contains("Epic 1", prompt);
        Assert.Contains("Product B", prompt);
    }

    [TestMethod]
    public void GenerateAiPrompt_CustomerFacingTemplate_ContainsRoadmapData()
    {
        // Arrange
        var lanes = BuildSampleLanes();

        // Act
        var prompt = _service.GenerateAiPrompt(lanes, RoadmapPromptTemplate.CustomerFacingRoadmap);

        // Assert
        Assert.Contains("customer", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Product A", prompt);
        Assert.Contains("What's Coming Next", prompt);
    }

    [TestMethod]
    public void GenerateAiPrompt_MilestoneTemplate_ContainsRoadmapData()
    {
        // Arrange
        var lanes = BuildSampleLanes();

        // Act
        var prompt = _service.GenerateAiPrompt(lanes, RoadmapPromptTemplate.MilestoneInfographic);

        // Assert
        Assert.Contains("milestone", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Product A", prompt);
        Assert.Contains("[TFS 100]", prompt);
    }

    [TestMethod]
    public void GenerateAiPrompt_EmptyLanes_ProducesPromptWithEmptyDataBlock()
    {
        // Arrange
        var lanes = new List<RoadmapProductEntry>();

        // Act
        var prompt = _service.GenerateAiPrompt(lanes, RoadmapPromptTemplate.ExecutiveRoadmap);

        // Assert — still produces a valid prompt, just no data
        Assert.Contains("executive", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void GenerateAiPrompt_NullLanes_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _service.GenerateAiPrompt(null!, RoadmapPromptTemplate.ExecutiveRoadmap));
    }

    [TestMethod]
    public void GenerateAiPrompt_InvalidTemplate_ThrowsArgumentOutOfRangeException()
    {
        var lanes = BuildSampleLanes();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            _service.GenerateAiPrompt(lanes, (RoadmapPromptTemplate)99));
    }

    #endregion

    #region GetAvailableTemplates

    [TestMethod]
    public void GetAvailableTemplates_ReturnsThreeTemplates()
    {
        var templates = RoadmapReportingService.GetAvailableTemplates();
        Assert.HasCount(3, templates);
    }

    [TestMethod]
    public void GetAvailableTemplates_AllHaveNameAndDescription()
    {
        var templates = RoadmapReportingService.GetAvailableTemplates();
        foreach (var t in templates)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(t.Name), $"Template {t.Template} has empty name.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(t.Description), $"Template {t.Template} has empty description.");
        }
    }

    #endregion

    #region Helpers

    private static List<RoadmapProductEntry> BuildSampleLanes() =>
    [
        new()
        {
            ProductName = "Product A",
            Epics = new List<RoadmapEpicEntry>
            {
                new() { Order = 1, Title = "Epic 1", TfsId = 100 },
                new() { Order = 2, Title = "Epic 2", TfsId = 200 }
            }
        },
        new()
        {
            ProductName = "Product B",
            Epics = new List<RoadmapEpicEntry>
            {
                new() { Order = 1, Title = "Epic 3", TfsId = 300 }
            }
        }
    ];

    #endregion
}
