using BookOwners.Domain.Enums;

namespace BookOwners.Domain.Entities;

public sealed record Book(string Name, BookType Type);