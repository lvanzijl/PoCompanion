using System.Reflection;
using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor;
using PoTool.Client.Services;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class OnboardingWizardImportTests
{
    [TestMethod]
    public async Task HandleImportedConfigurationAsync_SuccessfulImport_CompletesOnboardingAndClosesWizard()
    {
        var onboardingService = new Mock<IOnboardingService>(MockBehavior.Strict);
        onboardingService
            .Setup(service => service.MarkOnboardingCompletedAsync())
            .Returns(Task.CompletedTask);

        var dialog = new Mock<IMudDialogInstance>(MockBehavior.Strict);
        dialog
            .Setup(instance => instance.Close(It.IsAny<DialogResult>()))
            .Verifiable();

        var onCompletedCallCount = 0;
        var component = CreateWizardComponent(
            onboardingService.Object,
            dialog.Object,
            new EventCallbackFactory().Create(this, () =>
            {
                onCompletedCallCount++;
                return Task.CompletedTask;
            }));

        await InvokeHandleImportedConfigurationAsync(component, CreateImportResult(canImport: true, importExecuted: true));

        onboardingService.Verify(service => service.MarkOnboardingCompletedAsync(), Times.Once);
        dialog.Verify(instance => instance.Close(It.IsAny<DialogResult>()), Times.Once);
        Assert.AreEqual(1, onCompletedCallCount);
    }

    [TestMethod]
    public async Task HandleImportedConfigurationAsync_UnsuccessfulImport_DoesNotCompleteOnboarding()
    {
        var onboardingService = new Mock<IOnboardingService>(MockBehavior.Strict);
        var dialog = new Mock<IMudDialogInstance>(MockBehavior.Strict);
        var onCompletedCallCount = 0;

        var component = CreateWizardComponent(
            onboardingService.Object,
            dialog.Object,
            new EventCallbackFactory().Create(this, () =>
            {
                onCompletedCallCount++;
                return Task.CompletedTask;
            }));

        await InvokeHandleImportedConfigurationAsync(component, CreateImportResult(canImport: false, importExecuted: true));

        onboardingService.Verify(service => service.MarkOnboardingCompletedAsync(), Times.Never);
        dialog.Verify(instance => instance.Close(It.IsAny<DialogResult>()), Times.Never);
        Assert.AreEqual(0, onCompletedCallCount);
    }

    private static object CreateWizardComponent(
        IOnboardingService onboardingService,
        IMudDialogInstance dialog,
        EventCallback onCompleted)
    {
        var componentType = GetComponentType();
        var component = Activator.CreateInstance(componentType)
            ?? throw new InvalidOperationException("Could not create OnboardingWizard component instance.");

        SetProperty(componentType, component, "OnboardingService", onboardingService);
        SetProperty(componentType, component, "OnCompleted", onCompleted);
        SetProperty(componentType, component, "MudDialog", dialog);

        return component;
    }

    private static async Task InvokeHandleImportedConfigurationAsync(object component, ConfigurationImportResultDto result)
    {
        var method = GetComponentType().GetMethod(
            "HandleImportedConfigurationAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("HandleImportedConfigurationAsync method was not found.");

        var task = method.Invoke(component, [result]) as Task
            ?? throw new InvalidOperationException("HandleImportedConfigurationAsync did not return a Task.");

        await task;
    }

    private static Type GetComponentType()
    {
        return typeof(OnboardingService).Assembly.GetType("PoTool.Client.Components.Onboarding.OnboardingWizard", throwOnError: true)
            ?? throw new InvalidOperationException("OnboardingWizard component type was not found.");
    }

    private static void SetProperty(Type componentType, object component, string propertyName, object? value)
    {
        var property = componentType.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on OnboardingWizard.");

        property.SetValue(component, value);
    }

    private static ConfigurationImportResultDto CreateImportResult(bool canImport, bool importExecuted)
    {
        return new ConfigurationImportResultDto(
            CanImport: canImport,
            ImportExecuted: importExecuted,
            ExistingConfigurationDetected: false,
            RequiresDestructiveConfirmation: false,
            ProfilesValidated: [],
            ProfilesImported: [],
            ExistingConfigurationSummary: [],
            RemovedItems: [],
            Warnings: [],
            Errors: [],
            StructuredProfilesImported: [],
            ProductsImported: [],
            TeamsImported: [],
            RepositoriesLinked: [],
            GlobalSettingsApplied: []);
    }
}
