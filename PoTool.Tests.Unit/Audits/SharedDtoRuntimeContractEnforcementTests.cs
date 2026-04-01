using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Configuration;
using PoTool.Api.Filters;
using PoTool.Api.Persistence;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
public class SharedDtoRuntimeContractEnforcementTests
{
    [TestMethod]
    public void AllSharedDtoActionResultEndpoints_HaveRuntimeContractEnforcement()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddLogging();
        RegisterHostEnvironment(services);
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase("SharedDtoRuntimeContractEnforcement"));
        services.AddPoToolApiServices(configuration, isDevelopment: true);

        using var serviceProvider = services.BuildServiceProvider();
        var mvcOptions = serviceProvider.GetRequiredService<IOptions<MvcOptions>>().Value;
        var hasGlobalFilter = mvcOptions.Filters.OfType<EnforceSharedDtoActionResultContractFilter>().Any();

        Assert.IsTrue(hasGlobalFilter, "Global shared DTO result enforcement filter must be registered.");

        var missingEndpoints = typeof(Program).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(IsHttpAction)
            .Where(method => SharedDtoActionResultContractResolver.TryGetExpectedPayloadType(method, out _))
            .Where(method => !HasExplicitEnforcement(method) && !hasGlobalFilter)
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")
            .ToList();

        CollectionAssert.AreEqual(Array.Empty<string>(), missingEndpoints,
            "Every shared-DTO ActionResult endpoint must have explicit or global runtime contract enforcement.");
    }

    private static bool IsHttpAction(MethodInfo methodInfo)
    {
        return methodInfo.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any();
    }

    private static bool HasExplicitEnforcement(MethodInfo methodInfo)
    {
        return methodInfo.IsDefined(typeof(EnforceObjectResultTypeAttribute), inherit: true)
            || methodInfo.DeclaringType?.IsDefined(typeof(EnforceObjectResultTypeAttribute), inherit: true) == true;
    }

    private static void RegisterHostEnvironment(IServiceCollection services)
    {
        var hostEnvironmentMock = new Mock<IHostEnvironment>();
        hostEnvironmentMock.Setup(x => x.EnvironmentName).Returns("Development");
        hostEnvironmentMock.Setup(x => x.ApplicationName).Returns("PoTool.Tests");
        hostEnvironmentMock.Setup(x => x.ContentRootPath).Returns(AppContext.BaseDirectory);
        hostEnvironmentMock.Setup(x => x.ContentRootFileProvider).Returns(new NullFileProvider());
        services.AddSingleton(hostEnvironmentMock.Object);
    }
}
