using NJsonSchema;
using NJsonSchema.Generation;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace PoTool.Api.Configuration;

/// <summary>
/// Document processor that removes regex patterns and union types from integer properties in the OpenAPI spec.
/// This fixes the issue where integer properties were being generated as ["integer", "string"] with regex patterns,
/// causing the C# client generator to produce incorrect string default values for int properties.
/// </summary>
public class IntegerSchemaProcessor : IDocumentProcessor
{
    public void Process(DocumentProcessorContext context)
    {
        // Process all schemas in the document
        foreach (var schema in context.Document.Definitions.Values)
        {
            ProcessSchema(schema);
        }
        
        // Also process schemas in paths (request/response bodies)
        foreach (var path in context.Document.Paths.Values)
        {
            foreach (var operation in path.Values)
            {
                // Process parameters
                foreach (var parameter in operation.Parameters)
                {
                    ProcessSchema(parameter.Schema);
                }
                
                // Process responses
                foreach (var response in operation.Responses.Values)
                {
                    ProcessSchema(response.Schema);
                }
            }
        }
    }
    
    private void ProcessSchema(JsonSchema? schema)
    {
        if (schema == null) return;
        
        // Remove regex patterns from integer types
        if (schema.Type == JsonObjectType.Integer)
        {
            schema.Pattern = null;
        }
        
        // If the schema has multiple types including integer and string, simplify to just integer
        if (schema.Type.HasFlag(JsonObjectType.Integer) && 
            schema.Type.HasFlag(JsonObjectType.String))
        {
            schema.Type = JsonObjectType.Integer;
            schema.Pattern = null;
        }
        
        // Recursively process properties
        if (schema.Properties != null)
        {
            foreach (var property in schema.Properties.Values)
            {
                ProcessSchema(property);
            }
        }
        
        // Process array items
        if (schema.Item != null)
        {
            ProcessSchema(schema.Item);
        }
        
        // Process additional properties
        if (schema.AdditionalPropertiesSchema != null)
        {
            ProcessSchema(schema.AdditionalPropertiesSchema);
        }
        
        // Process oneOf, anyOf, allOf
        foreach (var subSchema in schema.OneOf)
        {
            ProcessSchema(subSchema);
        }
        foreach (var subSchema in schema.AnyOf)
        {
            ProcessSchema(subSchema);
        }
        foreach (var subSchema in schema.AllOf)
        {
            ProcessSchema(subSchema);
        }
    }
}
