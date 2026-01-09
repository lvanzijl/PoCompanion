using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Services;
using PoTool.Shared.Exceptions;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class ErrorMessageServiceTests
{
    private ErrorMessageService _errorMessageService = null!;

    [TestInitialize]
    public void Initialize()
    {
        _errorMessageService = new ErrorMessageService();
    }

    [TestMethod]
    public void GetErrorResponse_WithTfsAuthenticationException_ReturnsUserFriendlyMessage()
    {
        // Arrange
        var exception = new TfsAuthenticationException("Authentication failed", (string?)null);

        // Act
        var response = _errorMessageService.GetErrorResponse(exception);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("Authentication failed. Please check your Personal Access Token and ensure it has not expired.", response.UserMessage);
        Assert.IsNotNull(response.Suggestion);
        Assert.AreEqual(401, response.TechnicalDetails.StatusCode);
        Assert.AreEqual(nameof(TfsAuthenticationException), response.TechnicalDetails.ExceptionType);
    }

    [TestMethod]
    public void GetErrorResponse_WithTfsAuthorizationException_ReturnsUserFriendlyMessage()
    {
        // Arrange
        var exception = new TfsAuthorizationException("Access denied", (string?)null);

        // Act
        var response = _errorMessageService.GetErrorResponse(exception);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("Access denied. Please verify you have permission to access this resource.", response.UserMessage);
        Assert.IsNotNull(response.Suggestion);
        Assert.AreEqual(403, response.TechnicalDetails.StatusCode);
    }

    [TestMethod]
    public void GetErrorResponse_WithTfsRateLimitException_ReturnsUserFriendlyMessage()
    {
        // Arrange
        var exception = new TfsRateLimitException("Rate limit exceeded", (string?)null);

        // Act
        var response = _errorMessageService.GetErrorResponse(exception);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("Too many requests. Please wait a moment before trying again.", response.UserMessage);
        Assert.IsNotNull(response.Suggestion);
        Assert.AreEqual(429, response.TechnicalDetails.StatusCode);
    }

    [TestMethod]
    public void GetErrorResponse_WithTfsResourceNotFoundException_ReturnsUserFriendlyMessage()
    {
        // Arrange
        var exception = new TfsResourceNotFoundException("Resource not found", (string?)null);

        // Act
        var response = _errorMessageService.GetErrorResponse(exception);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("Resource not found. Please verify your configuration.", response.UserMessage);
        Assert.IsNotNull(response.Suggestion);
        Assert.AreEqual(404, response.TechnicalDetails.StatusCode);
    }

    [TestMethod]
    public void GetErrorResponse_WithHttpRequestException_ReturnsNetworkErrorMessage()
    {
        // Arrange
        var exception = new HttpRequestException("Connection refused");

        // Act
        var response = _errorMessageService.GetErrorResponse(exception);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("Network error occurred. Please check your connection.", response.UserMessage);
        Assert.IsNotNull(response.Suggestion);
        Assert.AreEqual(nameof(HttpRequestException), response.TechnicalDetails.ExceptionType);
    }

    [TestMethod]
    public void GetErrorResponse_WithTaskCanceledException_ReturnsTimeoutMessage()
    {
        // Arrange
        var exception = new TaskCanceledException("Operation timed out");

        // Act
        var response = _errorMessageService.GetErrorResponse(exception);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("The operation timed out or was cancelled.", response.UserMessage);
        Assert.IsNotNull(response.Suggestion);
    }

    [TestMethod]
    public void GetErrorResponse_WithGenericException_ReturnsGenericMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");

        // Act
        var response = _errorMessageService.GetErrorResponse(exception);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("An unexpected error occurred.", response.UserMessage);
        Assert.IsNotNull(response.Suggestion);
        Assert.AreEqual(nameof(InvalidOperationException), response.TechnicalDetails.ExceptionType);
    }

    [TestMethod]
    public void GetErrorResponse_WithContext_IncludesContextInMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");
        var context = "Error loading data";

        // Act
        var response = _errorMessageService.GetErrorResponse(exception, context);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("Error loading data: An unexpected error occurred.", response.UserMessage);
    }

    [TestMethod]
    public void GetErrorResponse_WithTfsException_MapsStatusCodeCorrectly()
    {
        // Arrange
        var exception = new TfsException("Server error", 500, null);

        // Act
        var response = _errorMessageService.GetErrorResponse(exception);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual("Server error occurred. Please try again later.", response.UserMessage);
        Assert.AreEqual(500, response.TechnicalDetails.StatusCode);
    }

    [TestMethod]
    public void GetErrorResponse_PreservesStackTrace()
    {
        // Arrange
        Exception? exception = null;
        try
        {
            throw new InvalidOperationException("Test exception");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Act
        var response = _errorMessageService.GetErrorResponse(exception!);

        // Assert
        Assert.IsNotNull(response.TechnicalDetails.StackTrace);
        Assert.AreNotEqual(0, response.TechnicalDetails.StackTrace!.Length);
    }

    [TestMethod]
    public void GetErrorResponse_AllHttpStatusCodes_ReturnUserFriendlyMessages()
    {
        // Arrange
        var statusCodes = new[] { 400, 401, 403, 404, 408, 429, 500, 502, 503, 504 };

        foreach (var statusCode in statusCodes)
        {
            // Arrange
            var exception = new TfsException($"Error {statusCode}", statusCode, null);

            // Act
            var response = _errorMessageService.GetErrorResponse(exception);

            // Assert
            Assert.IsNotNull(response, $"Response should not be null for status code {statusCode}");
            Assert.IsFalse(string.IsNullOrEmpty(response.UserMessage), $"UserMessage should not be empty for status code {statusCode}");
            Assert.DoesNotContain(statusCode.ToString(), response.UserMessage, $"UserMessage should not contain status code {statusCode} directly");
            Assert.IsNotNull(response.Suggestion, $"Suggestion should not be null for status code {statusCode}");
            Assert.AreEqual(statusCode, response.TechnicalDetails.StatusCode, $"StatusCode should match for {statusCode}");
        }
    }
}
