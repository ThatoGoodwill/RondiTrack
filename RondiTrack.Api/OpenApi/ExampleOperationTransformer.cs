using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace RondiTrack.Api.OpenApi;

// ---------------------------------------------------------------------------------
// Attaches a REALISTIC example request body to specific operations, by matching the
// route + HTTP method. Registered once in Program.cs (AddOpenApi -> AddOperationTransformer).
// Native .NET 10 OpenAPI extensibility -- no external package needed.
//
// Why this exists: the compiler only knows "string" and "decimal". A real contract also
// shows a HUMAN-BELIEVABLE example, so a caller never has to guess field names or formats
// from a placeholder like "string" or "0".
// ---------------------------------------------------------------------------------
public sealed class ExampleOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var route = context.Description.RelativePath ?? "";
        var method = context.Description.HttpMethod;

        if (route == "api/users" && method == "POST")
            AddRequestExample(operation, new
            {
                firstName = "Thandi",
                lastName = "Nkosi",
                email = "thandi.nkosi@example.com",
                dateOfBirth = "1988-03-14"
            });

        if (route == "api/stokvels" && method == "POST")
            AddRequestExample(operation, new
            {
                name = "Ubuntu Savers",
                contributionAmount = 500.00,
                frequency = "Monthly",
                maxMembers = 10
            });

        if (route == "api/stokvels/{stokvelId}/members" && method == "POST")
            AddRequestExample(operation, new
            {
                userId = "01a0cbf6-9ce7-7d31-b6e0-111672c547ab"
            });

        if (route == "api/stokvels/{stokvelId}/cycles" && method == "POST")
            AddRequestExample(operation, new
            {
                label = "2026-09",
                targetAmount = 500.00
            });

        if (route == "api/stokvels/{stokvelId}/contributions" && method == "POST")
            AddRequestExample(operation, new
            {
                userId = "01a0cbf6-9ce7-7d31-b6e0-111672c547ab",
                contributionCycleId = "01a0cc0d-0ba7-7508-a89d-a7549cb4af62",
                amount = 500.00
            });

        return Task.CompletedTask;
    }

    private static void AddRequestExample(OpenApiOperation operation, object example)
    {
        var json = System.Text.Json.JsonSerializer.SerializeToNode(example);
        if (operation.RequestBody?.Content is { } content)
            foreach (var media in content.Values)
                media.Example = json;
    }
}
