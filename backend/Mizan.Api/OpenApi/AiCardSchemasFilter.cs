using Microsoft.OpenApi;
using Mizan.Contracts.Ai;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Mizan.Api.OpenApi;

/// <summary>
/// A card's <c>data</c> crosses the wire as JSON whose shape its <c>type</c> names, so no route
/// refers to the card records and the generator would leave them out. This adds each record of
/// <see cref="AiCardTypes.DataTypes"/> to the document, so the web client's generated types carry
/// the exact shape every card component renders.
/// </summary>
public sealed class AiCardSchemasFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var type in AiCardTypes.DataTypes.Values)
        {
            context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository);
        }
    }
}
