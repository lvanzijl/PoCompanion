using FluentValidation.TestHelper;
using PoTool.Core.Settings.Commands;
using PoTool.Core.Settings.Validators;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Validators;

[TestClass]
public class CreateProductCommandValidatorTests
{
    private CreateProductCommandValidator _validator = null!;

    [TestInitialize]
    public void Setup()
    {
        _validator = new CreateProductCommandValidator();
    }

    [TestMethod]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductOwnerId: 1,
            Name: "Test Product",
            BacklogRootWorkItemIds: new List<int> { 100 }
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public void Validate_EmptyName_ShouldFail()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductOwnerId: 1,
            Name: "",
            BacklogRootWorkItemIds: new List<int> { 100 }
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Product name is required");
    }

    [TestMethod]
    public void Validate_NameTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductOwnerId: 1,
            Name: new string('a', 201),
            BacklogRootWorkItemIds: new List<int> { 100 }
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Product name must not exceed 200 characters");
    }

    [TestMethod]
    public void Validate_EmptyBacklogRootIds_ShouldFail()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductOwnerId: 1,
            Name: "Test Product",
            BacklogRootWorkItemIds: new List<int>()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BacklogRootWorkItemIds)
            .WithErrorMessage("At least one backlog root work item ID is required");
    }

    [TestMethod]
    public void Validate_MultipleBacklogRootIds_ShouldPass()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductOwnerId: 1,
            Name: "Test Product",
            BacklogRootWorkItemIds: new List<int> { 100, 200, 300 }
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public void Validate_InvalidBacklogRootId_ShouldFail()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductOwnerId: 1,
            Name: "Test Product",
            BacklogRootWorkItemIds: new List<int> { 0 }
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BacklogRootWorkItemIds)
            .WithErrorMessage("All backlog root work item IDs must be greater than 0");
    }

    [TestMethod]
    public void Validate_InvalidProductOwnerId_ShouldFail()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductOwnerId: 0,
            Name: "Test Product",
            BacklogRootWorkItemIds: new List<int> { 100 }
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ProductOwnerId)
            .WithErrorMessage("Product owner ID must be greater than 0");
    }

    [TestMethod]
    public void Validate_DefaultPictureOutOfRange_ShouldFail()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductOwnerId: 1,
            Name: "Test Product",
            BacklogRootWorkItemIds: new List<int> { 100 },
            PictureType: ProductPictureType.Default,
            DefaultPictureId: 64
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DefaultPictureId)
            .WithErrorMessage("Default picture ID must be between 0 and 63");
    }

    [TestMethod]
    public void Validate_CustomPictureWithoutPath_ShouldFail()
    {
        // Arrange
        var command = new CreateProductCommand(
            ProductOwnerId: 1,
            Name: "Test Product",
            BacklogRootWorkItemIds: new List<int> { 100 },
            PictureType: ProductPictureType.Custom,
            CustomPicturePath: null
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CustomPicturePath)
            .WithErrorMessage("Custom picture path is required when picture type is Custom");
    }
}
