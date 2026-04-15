using BookOwners.Domain.Enums;

namespace BookOwners.Domain.Entities;

public sealed record BookOwner(string Name, int Age, IReadOnlyList<Book> Books)
{
    private const int AdultAgeThreshold = 18;

    public AgeCategory AgeCategory =>
        Age >= AdultAgeThreshold ? AgeCategory.Adults : AgeCategory.Children;
}