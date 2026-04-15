using BookOwners.Application.Interfaces;
using BookOwners.Domain.Entities;
using BookOwners.Domain.Enums;

namespace BookOwners.Application.Services;

public sealed class BookGroupingService : IBookGroupingService
{
    public IReadOnlyList<AgeCategoryGrouping> GroupByAgeCategory(
        IReadOnlyList<BookOwner> owners,
        BookType? filterByType = null)
    {
        var allCategories = Enum.GetValues<AgeCategory>().OrderBy(c => c);

        var grouped = owners
            .GroupBy(o => o.AgeCategory)
            .ToDictionary(g => g.Key, g => g.ToList());

        return allCategories
            .Select(category =>
            {
                var ownersInCategory = grouped.TryGetValue(category, out var list) ? list : [];

                var books = ownersInCategory
                    .SelectMany(o => o.Books)
                    .Where(b => filterByType is null || b.Type == filterByType)
                    .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    .AsReadOnly();

                return new AgeCategoryGrouping(category, books);
            })
            .Where(g => g.Books.Count > 0)
            .ToList()
            .AsReadOnly();
    }
}