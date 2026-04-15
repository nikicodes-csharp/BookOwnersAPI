using BookOwners.Domain.Enums;

namespace BookOwners.Domain.Entities;

public sealed record AgeCategoryGrouping(AgeCategory Category, IReadOnlyList<Book> Books);