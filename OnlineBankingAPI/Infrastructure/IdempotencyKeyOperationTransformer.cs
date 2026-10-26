using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OnlineBankingAPI.Infrastructure;

/// <summary>Documents the required Idempotency-Key header on actions marked [RequireIdempotencyKey].</summary>
public class IdempotencyKeyOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<RequireIdempotencyKeyAttribute>().Any())
            return Task.CompletedTask;

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = RequireIdempotencyKeyAttribute.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "A UUID chosen by the client. Reuse it, with the same body, to retry safely.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
        });
        return Task.CompletedTask;
    }
}
