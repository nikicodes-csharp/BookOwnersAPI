using BookOwners.Application.DTOs;
using BookOwners.Application.Interfaces;
using BookOwners.Domain.Enums;

namespace BookOwners.Application.Services;

public sealed class BookService : IBookService
{
    private readonly IBookOwnerRepository _repository;
    private readonly IBookGroupingService _groupingService;

    public BookService(IBookOwnerRepository repository, IBookGroupingService groupingService)
    {
        _repository = repository;
        _groupingService = groupingService;
    }

    public async Task<GetBooksResponse> GetBooksAsync(
        bool hardcoverOnly = false,
        CancellationToken cancellationToken = default)
    {
        var owners = await _repository.GetAllAsync(cancellationToken);

        var filter = hardcoverOnly ? BookType.Hardcover : (BookType?)null;
        var groupings = _groupingService.GroupByAgeCategory(owners, filter);

        var dtos = groupings
            .Select(g => new AgeCategoryGroupingDto(
                Category: g.Category.ToString(),
                Books: g.Books
                    .Select(b => new BookDto(b.Name, b.Type.ToString()))
                    .ToList()
                    .AsReadOnly()))
            .ToList()
            .AsReadOnly();

        return new GetBooksResponse(dtos);
    }
}