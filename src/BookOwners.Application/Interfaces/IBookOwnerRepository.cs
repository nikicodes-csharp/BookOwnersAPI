using BookOwners.Domain.Entities;

namespace BookOwners.Application.Interfaces;

public interface IBookOwnerRepository
{
    Task<IReadOnlyList<BookOwner>> GetAllAsync(CancellationToken cancellationToken = default);
}