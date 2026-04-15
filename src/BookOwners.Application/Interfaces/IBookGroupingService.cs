using BookOwners.Domain.Entities;
using BookOwners.Domain.Enums;

namespace BookOwners.Application.Interfaces;

public interface IBookGroupingService
{
    IReadOnlyList<AgeCategoryGrouping> GroupByAgeCategory(
        IReadOnlyList<BookOwner> owners,
        BookType? filterByType = null);
}