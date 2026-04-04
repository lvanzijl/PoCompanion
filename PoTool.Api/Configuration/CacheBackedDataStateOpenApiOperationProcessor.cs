using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using PoTool.Api.Filters;
using PoTool.Shared.DataState;

namespace PoTool.Api.Configuration;

public sealed class CacheBackedDataStateOpenApiOperationProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var path = context.OperationDescription.Path;
        if (!DataSourceModeConfiguration.RequiresCache(path))
        {
            return true;
        }

        if (!SharedDtoActionResultContractResolver.TryGetDeclaredPayloadType(context.MethodInfo, out var payloadType))
        {
            return true;
        }

        var wrappedPayloadType = typeof(DataStateResponseDto<>).MakeGenericType(payloadType);
        var wrappedSchema = context.SchemaGenerator.Generate(wrappedPayloadType, context.SchemaResolver);

        if (!context.OperationDescription.Operation.Responses.TryGetValue("200", out var successResponse))
        {
            successResponse = new OpenApiResponse();
            context.OperationDescription.Operation.Responses["200"] = successResponse;
        }

        successResponse.Description = "Cache-backed response envelope. Inspect state for available, empty, not-ready, or failed outcomes.";
        successResponse.Content.Clear();
        successResponse.Content["application/json"] = new OpenApiMediaType
        {
            Schema = new JsonSchema
            {
                Reference = wrappedSchema.ActualSchema
            }
        };

        RemoveNormalizedResponses(context.OperationDescription.Operation.Responses);
        return true;
    }

    private static void RemoveNormalizedResponses(IDictionary<string, OpenApiResponse> responses)
    {
        foreach (var statusCode in responses.Keys.ToArray())
        {
            if (statusCode is "204" or "404")
            {
                responses.Remove(statusCode);
                continue;
            }

            if (int.TryParse(statusCode, out var parsedStatusCode) &&
                parsedStatusCode >= 500 &&
                parsedStatusCode <= 599)
            {
                responses.Remove(statusCode);
            }
        }
    }
}
