namespace BookOwners.Application.DTOs;

public sealed record BookDto(string Name, string Type);

public sealed record AgeCategoryGroupingDto(string Category, IReadOnlyList<BookDto> Books);

public sealed record GetBooksResponse(IReadOnlyList<AgeCategoryGroupingDto> Groups);