namespace PoTool.Api.Configuration;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DataSourceModeAttribute : Attribute
{
    public DataSourceModeAttribute(RouteIntent routeIntent)
    {
        RouteIntent = routeIntent;
    }

    public RouteIntent RouteIntent { get; }
}
