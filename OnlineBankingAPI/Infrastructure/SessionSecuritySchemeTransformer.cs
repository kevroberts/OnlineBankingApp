using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OnlineBankingAPI.Services;

namespace OnlineBankingAPI.Infrastructure;

/// <summary>Describes the obk_session cookie and its bearer alternative in /openapi/v1.json.</summary>
public class SessionSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = "OnlineBankingAPI";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["sessionCookie"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = SessionService.CookieName,
        };
        document.Components.SecuritySchemes["bearerSession"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "The same opaque session token, for non-browser clients",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("sessionCookie", document)] = [],
        });
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("bearerSession", document)] = [],
        });
        return Task.CompletedTask;
    }
}
