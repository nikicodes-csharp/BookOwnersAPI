using System.Text.Json.Serialization;

namespace BookOwners.Infrastructure.Http;

internal sealed record BookOwnerApiResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("age")] int Age,
    [property: JsonPropertyName("books")] List<BookApiResponse> Books);

internal sealed record BookApiResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type);