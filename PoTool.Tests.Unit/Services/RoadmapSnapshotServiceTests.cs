using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class RoadmapSnapshotServiceTests
{
    private RoadmapSnapshotService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new RoadmapSnapshotService();
    }

    #region CreateSnapshot

    [TestMethod]
    public void CreateSnapshot_EmptyLanes_ReturnsEmptySnapshot()
    {
        // Arrange
        var lanes = Array.Empty<RoadmapProductEntry>().ToList();

        // Act
        var snapshot = _service.CreateSnapshot(lanes);

        // Assert
        Assert.IsNotNull(snapshot.Id);
        Assert.AreNotEqual(string.Empty, snapshot.Id);
        Assert.IsEmpty(snapshot.Products);
        Assert.IsNull(snapshot.Description);
    }

    [TestMethod]
    public void CreateSnapshot_WithLanes_CapturesAllProductsAndEpics()
    {
        // Arrange
        var lanes = BuildSampleLanes();

        // Act
        var snapshot = _service.CreateSnapshot(lanes, "Test snapshot");

        // Assert
        Assert.AreEqual("Test snapshot", snapshot.Description);
        Assert.HasCount(2, snapshot.Products);
        Assert.AreEqual("Product A", snapshot.Products[0].ProductName);
        Assert.HasCount(2, snapshot.Products[0].Epics);
        Assert.AreEqual(1, snapshot.Products[0].Epics[0].Order);
        Assert.AreEqual("Epic 1", snapshot.Products[0].Epics[0].Title);
        Assert.AreEqual(100, snapshot.Products[0].Epics[0].TfsId);
    }

    [TestMethod]
    public void CreateSnapshot_GeneratesUniqueIds()
    {
        var lanes = BuildSampleLanes();
        var snap1 = _service.CreateSnapshot(lanes);
        var snap2 = _service.CreateSnapshot(lanes);

        Assert.AreNotEqual(snap1.Id, snap2.Id);
    }

    [TestMethod]
    public void CreateSnapshot_NullLanes_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _service.CreateSnapshot(null!));
    }

    #endregion

    #region Compare — No Drift

    [TestMethod]
    public void Compare_IdenticalState_NoDrift()
    {
        // Arrange
        var lanes = BuildSampleLanes();
        var snapshot = _service.CreateSnapshot(lanes);

        // Act
        var drift = _service.Compare(snapshot, lanes);

        // Assert
        Assert.IsFalse(drift.HasDrift);
        Assert.HasCount(2, drift.Products);
        Assert.IsFalse(drift.Products[0].HasDrift);
        Assert.IsFalse(drift.Products[1].HasDrift);
    }

    #endregion

    #region Compare — Order Changes

    [TestMethod]
    public void Compare_EpicMovedEarlier_DetectsMovedEarlier()
    {
        // Arrange — snapshot: Epic 1 at #1, Epic 2 at #2
        var snapshotLanes = BuildSampleLanes();
        var snapshot = _service.CreateSnapshot(snapshotLanes);

        // Current: Epic 2 moved to #1, Epic 1 moved to #2
        var currentLanes = new List<RoadmapProductEntry>
        {
            new()
            {
                ProductName = "Product A",
                Epics = new List<RoadmapEpicEntry>
                {
                    new() { Order = 1, Title = "Epic 2", TfsId = 200 },
                    new() { Order = 2, Title = "Epic 1", TfsId = 100 }
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
        };

        // Act
        var drift = _service.Compare(snapshot, currentLanes);

        // Assert
        Assert.IsTrue(drift.HasDrift);
        var productA = drift.Products.First(p => p.ProductName == "Product A");
        Assert.IsTrue(productA.HasDrift);

        var epic1 = productA.Epics.First(e => e.TfsId == 100);
        Assert.AreEqual(DriftChangeType.MovedLater, epic1.ChangeType);
        Assert.AreEqual(1, epic1.SnapshotOrder);
        Assert.AreEqual(2, epic1.CurrentOrder);

        var epic2 = productA.Epics.First(e => e.TfsId == 200);
        Assert.AreEqual(DriftChangeType.MovedEarlier, epic2.ChangeType);
        Assert.AreEqual(2, epic2.SnapshotOrder);
        Assert.AreEqual(1, epic2.CurrentOrder);
    }

    #endregion

    #region Compare — Added Epics

    [TestMethod]
    public void Compare_NewEpicAdded_DetectsAdded()
    {
        // Arrange — snapshot has 2 epics for Product A
        var snapshotLanes = BuildSampleLanes();
        var snapshot = _service.CreateSnapshot(snapshotLanes);

        // Current has 3 epics (new epic 400 added)
        var currentLanes = new List<RoadmapProductEntry>
        {
            new()
            {
                ProductName = "Product A",
                Epics = new List<RoadmapEpicEntry>
                {
                    new() { Order = 1, Title = "Epic 1", TfsId = 100 },
                    new() { Order = 2, Title = "Epic 2", TfsId = 200 },
                    new() { Order = 3, Title = "Epic New", TfsId = 400 }
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
        };

        // Act
        var drift = _service.Compare(snapshot, currentLanes);

        // Assert
        Assert.IsTrue(drift.HasDrift);
        var productA = drift.Products.First(p => p.ProductName == "Product A");
        var addedEpic = productA.Epics.First(e => e.TfsId == 400);
        Assert.AreEqual(DriftChangeType.Added, addedEpic.ChangeType);
        Assert.IsNull(addedEpic.SnapshotOrder);
        Assert.AreEqual(3, addedEpic.CurrentOrder);
    }

    #endregion

    #region Compare — Removed Epics

    [TestMethod]
    public void Compare_EpicRemoved_DetectsRemoved()
    {
        // Arrange — snapshot has 2 epics for Product A
        var snapshotLanes = BuildSampleLanes();
        var snapshot = _service.CreateSnapshot(snapshotLanes);

        // Current has only 1 epic (Epic 2 removed)
        var currentLanes = new List<RoadmapProductEntry>
        {
            new()
            {
                ProductName = "Product A",
                Epics = new List<RoadmapEpicEntry>
                {
                    new() { Order = 1, Title = "Epic 1", TfsId = 100 }
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
        };

        // Act
        var drift = _service.Compare(snapshot, currentLanes);

        // Assert
        Assert.IsTrue(drift.HasDrift);
        var productA = drift.Products.First(p => p.ProductName == "Product A");
        var removedEpic = productA.Epics.First(e => e.TfsId == 200);
        Assert.AreEqual(DriftChangeType.Removed, removedEpic.ChangeType);
        Assert.AreEqual(2, removedEpic.SnapshotOrder);
        Assert.IsNull(removedEpic.CurrentOrder);
    }

    #endregion

    #region Compare — Title Change Only

    [TestMethod]
    public void Compare_TitleChangedButSameOrder_NoOrderDrift()
    {
        // Arrange — snapshot with specific titles
        var snapshotLanes = BuildSampleLanes();
        var snapshot = _service.CreateSnapshot(snapshotLanes);

        // Current with same TFS IDs and order but different titles
        var currentLanes = new List<RoadmapProductEntry>
        {
            new()
            {
                ProductName = "Product A",
                Epics = new List<RoadmapEpicEntry>
                {
                    new() { Order = 1, Title = "Epic 1 Renamed", TfsId = 100 },
                    new() { Order = 2, Title = "Epic 2 Renamed", TfsId = 200 }
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
        };

        // Act
        var drift = _service.Compare(snapshot, currentLanes);

        // Assert — no drift because order unchanged, ID-based comparison
        Assert.IsFalse(drift.HasDrift);
        var epic1 = drift.Products[0].Epics.First(e => e.TfsId == 100);
        Assert.AreEqual(DriftChangeType.Unchanged, epic1.ChangeType);
        Assert.AreEqual("Epic 1 Renamed", epic1.Title); // Uses current title
    }

    #endregion

    #region Compare — New Product In Current

    [TestMethod]
    public void Compare_NewProductInCurrent_AllEpicsMarkedAdded()
    {
        // Arrange — snapshot has 2 products
        var snapshotLanes = BuildSampleLanes();
        var snapshot = _service.CreateSnapshot(snapshotLanes);

        // Current has an extra product
        var currentLanes = new List<RoadmapProductEntry>(BuildSampleLanes())
        {
            new()
            {
                ProductName = "Product C",
                Epics = new List<RoadmapEpicEntry>
                {
                    new() { Order = 1, Title = "Epic C1", TfsId = 500 }
                }
            }
        };

        // Act
        var drift = _service.Compare(snapshot, currentLanes);

        // Assert
        Assert.IsTrue(drift.HasDrift);
        var productC = drift.Products.First(p => p.ProductName == "Product C");
        Assert.IsTrue(productC.HasDrift);
        Assert.HasCount(1, productC.Epics);
        Assert.AreEqual(DriftChangeType.Added, productC.Epics[0].ChangeType);
    }

    #endregion

    #region Compare — Empty Snapshot

    [TestMethod]
    public void Compare_EmptySnapshot_AllCurrentEpicsAdded()
    {
        var emptyLanes = new List<RoadmapProductEntry>();
        var snapshot = _service.CreateSnapshot(emptyLanes);

        var currentLanes = BuildSampleLanes();
        var drift = _service.Compare(snapshot, currentLanes);

        Assert.IsTrue(drift.HasDrift);
        Assert.HasCount(2, drift.Products);
        Assert.IsTrue(drift.Products.All(p => p.Epics.All(e => e.ChangeType == DriftChangeType.Added)));
    }

    #endregion

    #region Compare — Null Arguments

    [TestMethod]
    public void Compare_NullSnapshot_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _service.Compare(null!, BuildSampleLanes()));
    }

    [TestMethod]
    public void Compare_NullCurrentLanes_ThrowsArgumentNullException()
    {
        var snapshot = _service.CreateSnapshot(BuildSampleLanes());
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _service.Compare(snapshot, null!));
    }

    #endregion

    #region Compare — Mixed Changes

    [TestMethod]
    public void Compare_MixedChanges_DetectsAllDriftTypes()
    {
        // Arrange — snapshot: Epic 1 #1, Epic 2 #2, Epic 3 #3
        var snapshotLanes = new List<RoadmapProductEntry>
        {
            new()
            {
                ProductName = "Product A",
                Epics = new List<RoadmapEpicEntry>
                {
                    new() { Order = 1, Title = "Epic 1", TfsId = 100 },
                    new() { Order = 2, Title = "Epic 2", TfsId = 200 },
                    new() { Order = 3, Title = "Epic 3", TfsId = 300 }
                }
            }
        };
        var snapshot = _service.CreateSnapshot(snapshotLanes);

        // Current: Epic 2 removed, Epic 3 moved to #1, Epic 1 stayed at #2, Epic 4 added at #3
        var currentLanes = new List<RoadmapProductEntry>
        {
            new()
            {
                ProductName = "Product A",
                Epics = new List<RoadmapEpicEntry>
                {
                    new() { Order = 1, Title = "Epic 3", TfsId = 300 },
                    new() { Order = 2, Title = "Epic 1", TfsId = 100 },
                    new() { Order = 3, Title = "Epic 4", TfsId = 400 }
                }
            }
        };

        // Act
        var drift = _service.Compare(snapshot, currentLanes);

        // Assert
        Assert.IsTrue(drift.HasDrift);
        var product = drift.Products[0];

        var epic1 = product.Epics.First(e => e.TfsId == 100);
        Assert.AreEqual(DriftChangeType.MovedLater, epic1.ChangeType);

        var epic2 = product.Epics.First(e => e.TfsId == 200);
        Assert.AreEqual(DriftChangeType.Removed, epic2.ChangeType);

        var epic3 = product.Epics.First(e => e.TfsId == 300);
        Assert.AreEqual(DriftChangeType.MovedEarlier, epic3.ChangeType);

        var epic4 = product.Epics.First(e => e.TfsId == 400);
        Assert.AreEqual(DriftChangeType.Added, epic4.ChangeType);
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
