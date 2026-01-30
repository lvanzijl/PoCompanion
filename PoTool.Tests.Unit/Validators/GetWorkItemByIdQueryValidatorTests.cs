using FluentValidation.TestHelper;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Tests.Unit.Validators;

[TestClass]
public class GetWorkItemByIdQueryValidatorTests
{
    private GetWorkItemByIdQueryValidator _validator = null!;

    [TestInitialize]
    public void Setup()
    {
        _validator = new GetWorkItemByIdQueryValidator();
    }

    [TestMethod]
    public void Validate_ValidId_ShouldPass()
    {
        // Arrange
        var query = new GetWorkItemByIdQuery(TfsId: 100);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public void Validate_ZeroId_ShouldFail()
    {
        // Arrange
        var query = new GetWorkItemByIdQuery(TfsId: 0);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TfsId)
            .WithErrorMessage("Work item ID must be greater than 0");
    }

    [TestMethod]
    public void Validate_NegativeId_ShouldFail()
    {
        // Arrange
        var query = new GetWorkItemByIdQuery(TfsId: -1);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TfsId)
            .WithErrorMessage("Work item ID must be greater than 0");
    }
}
