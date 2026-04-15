using BookOwners.Application.DTOs;

namespace BookOwners.Application.Interfaces;

public interface IBookService
{
    Task<GetBooksResponse> GetBooksAsync(bool hardcoverOnly = false, CancellationToken cancellationToken = default);
}