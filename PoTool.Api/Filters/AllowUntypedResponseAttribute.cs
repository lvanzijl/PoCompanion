namespace PoTool.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class AllowUntypedResponseAttribute : Attribute
{
}
