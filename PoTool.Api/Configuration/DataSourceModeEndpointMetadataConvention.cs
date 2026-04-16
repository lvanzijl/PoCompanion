using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace PoTool.Api.Configuration;

public sealed class DataSourceModeEndpointMetadataConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        foreach (var controller in application.Controllers)
        {
            var controllerRouteIntent = controller.Attributes.OfType<DataSourceModeAttribute>().LastOrDefault()?.RouteIntent;

            foreach (var action in controller.Actions)
            {
                var actionRouteIntent = action.Attributes.OfType<DataSourceModeAttribute>().LastOrDefault()?.RouteIntent;
                var resolvedRouteIntent = actionRouteIntent ?? controllerRouteIntent;
                if (resolvedRouteIntent is null)
                {
                    continue;
                }

                foreach (var selector in action.Selectors)
                {
                    selector.EndpointMetadata.Add(new DataSourceModeMetadata(resolvedRouteIntent.Value));
                }
            }
        }
    }
}
