using System.Text.Json;
using BookOwners.Application.Interfaces;
using BookOwners.Domain.Entities;
using BookOwners.Domain.Enums;
using BookOwners.Infrastructure.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BookOwners.Infrastructure.Services;

public sealed class BookOwnerRepository : IBookOwnerRepository
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BookOwnerRepository> _logger;

    private const string CacheKey = "book_owners_raw";
    private const int MaxRetries = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BookOwnerRepository(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<BookOwnerRepository> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BookOwner>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Return cached raw owners immediately, filtering is done by BookGroupingService
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<BookOwner>? cached) && cached is { Count: > 0 })
        {
            _logger.LogInformation("Returning {Count} book owners from cache", cached.Count);
            return cached;
        }

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            _logger.LogInformation("Fetching book owners from upstream API (attempt {Attempt})", attempt);

            try
            {
                var response = await _httpClient.GetAsync("api/v1/bookowners", cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Upstream API returned empty body on attempt {Attempt}", attempt);
                    await DelayIfNotLast(attempt, cancellationToken);
                    continue;
                }

                var apiModels = JsonSerializer.Deserialize<List<BookOwnerApiResponse>>(content, JsonOptions) ?? [];

                if (apiModels.Count == 0)
                {
                    _logger.LogWarning("Upstream API returned empty list on attempt {Attempt}", attempt);
                    await DelayIfNotLast(attempt, cancellationToken);
                    continue;
                }

                var owners = apiModels.Select(MapToDomain).ToList().AsReadOnly();

                // Validate both age categories are present before caching.
                // The upstream Bupa API occasionally returns partial data (e.g. only Children owners).
                // Caching incomplete data would cause one category to disappear until cache expires.
                var hasAdults = owners.Any(o => o.AgeCategory == AgeCategory.Adults);
                var hasChildren = owners.Any(o => o.AgeCategory == AgeCategory.Children);

                if (!hasAdults || !hasChildren)
                {
                    _logger.LogWarning(
                        "Upstream API returned incomplete data on attempt {Attempt} " +
                        "(Adults: {HasAdults}, Children: {HasChildren}) — retrying",
                        attempt, hasAdults, hasChildren);
                    await DelayIfNotLast(attempt, cancellationToken);
                    continue;
                }

                _cache.Set(CacheKey, owners, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(5)
                });

                _logger.LogInformation("Retrieved {Count} book owners from API, cache updated", owners.Count);
                return owners;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} failed with HTTP error, retrying...", attempt);
                await DelayIfNotLast(attempt, cancellationToken);
            }
            catch (JsonException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} failed with JSON parse error, retrying...", attempt);
                await DelayIfNotLast(attempt, cancellationToken);
            }
        }

        // All retries exhausted, serve stale cache if available
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<BookOwner>? stale) && stale is { Count: > 0 })
        {
            _logger.LogWarning("Upstream API unreliable, serving stale cache ({Count} owners)", stale.Count);
            return stale;
        }

        _logger.LogError("Upstream API failed and no cache available, returning empty list");
        return [];
    }

    private static async Task DelayIfNotLast(int attempt, CancellationToken cancellationToken)
    {
        if (attempt < MaxRetries)
            await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
    }

    private static BookOwner MapToDomain(BookOwnerApiResponse model)
    {
        var books = (model.Books ?? [])
            .Select(b => new Book(Name: b.Name, Type: ParseBookType(b.Type)))
            .ToList()
            .AsReadOnly();

        return new BookOwner(model.Name, model.Age, books);
    }

    private static BookType ParseBookType(string raw) =>
        Enum.TryParse<BookType>(raw, ignoreCase: true, out var result)
            ? result
            : BookType.Paperback;
}