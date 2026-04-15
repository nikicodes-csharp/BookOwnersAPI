using System.Net;
using System.Net.Http.Json;
using BookOwners.Application.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace BookOwners.IntegrationTests;

/// <summary>
/// Spins up a real ASP.NET Core test server and uses WireMock to stub the upstream Bupa API.
/// Each test stubs WireMock first, then creates a client with a fresh isolated IMemoryCache.
/// </summary>
[TestFixture]
public sealed class BooksControllerIntegrationTests
{
    private WireMockServer _wireMock = null!;

    private const string BookOwnersJson = """
        [
          { "name": "Jane", "age": 23, "books": [
              { "name": "Hamlet", "type": "Hardcover" },
              { "name": "Wuthering Heights", "type": "Paperback" }
          ]},
          { "name": "Charlotte", "age": 14, "books": [
              { "name": "Hamlet", "type": "Paperback" }
          ]},
          { "name": "Max", "age": 25, "books": [
              { "name": "React: The Ultimate Guide", "type": "Hardcover" },
              { "name": "Gulliver's Travels", "type": "Hardcover" },
              { "name": "Jane Eyre", "type": "Paperback" },
              { "name": "Great Expectations", "type": "Hardcover" }
          ]},
          { "name": "William", "age": 15, "books": [
              { "name": "Great Expectations", "type": "Hardcover" }
          ]},
          { "name": "Charles", "age": 17, "books": [
              { "name": "Little Red Riding Hood", "type": "Hardcover" },
              { "name": "The Hobbit", "type": "Ebook" }
          ]}
        ]
        """;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _wireMock = WireMockServer.Start();
        // Default stub active for all tests unless overridden
        StubUpstreamWithFullData();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _wireMock.Stop();

    [SetUp]
    public void SetUp()
    {
        // Reset and re-apply the default stub before every test
        _wireMock.Reset();
        StubUpstreamWithFullData();
    }

    [Test]
    public async Task GetBooks_NoFilter_Returns200WithTwoGroups()
    {
        using var client = CreateFreshClient();

        var response = await client.GetAsync("/api/v1/books");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();
        body!.Groups.Should().HaveCount(2);
        body.Groups.Should().Contain(g => g.Category == "Adults");
        body.Groups.Should().Contain(g => g.Category == "Children");
    }

    [Test]
    public async Task GetBooks_NoFilter_AdultBooksAreSortedAlphabetically()
    {
        using var client = CreateFreshClient();

        var response = await client.GetAsync("/api/v1/books");
        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();

        var adultGroup = body!.Groups.FirstOrDefault(g => g.Category == "Adults");
        adultGroup.Should().NotBeNull("Adults group should be present");
        adultGroup!.Books.Select(b => b.Name).Should().BeInAscendingOrder();
    }

    [Test]
    public async Task GetBooks_NoFilter_ChildrenBooksAreSortedAlphabetically()
    {
        using var client = CreateFreshClient();

        var response = await client.GetAsync("/api/v1/books");
        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();

        var childGroup = body!.Groups.FirstOrDefault(g => g.Category == "Children");
        childGroup.Should().NotBeNull("Children group should be present");
        childGroup!.Books.Select(b => b.Name).Should().BeInAscendingOrder();
    }

    [Test]
    public async Task GetBooks_HardcoverOnly_ReturnsOnlyHardcoverBooks()
    {
        using var client = CreateFreshClient();

        var response = await client.GetAsync("/api/v1/books?hardcoverOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();
        body!.Groups.SelectMany(g => g.Books)
            .Should().AllSatisfy(b => b.Type.Should().Be("Hardcover"));
    }

    [Test]
    public async Task GetBooks_HardcoverOnly_ExcludesEbookAndPaperback()
    {
        using var client = CreateFreshClient();

        var response = await client.GetAsync("/api/v1/books?hardcoverOnly=true");
        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();

        var allBooks = body!.Groups.SelectMany(g => g.Books).ToList();
        allBooks.Should().NotContain(b => b.Name == "The Hobbit");
        allBooks.Should().NotContain(b => b.Name == "Wuthering Heights");
        allBooks.Should().NotContain(b => b.Name == "Jane Eyre");
        allBooks.Should().NotContain(b => b.Name == "Hamlet" && b.Type == "Paperback");
    }

    [Test]
    public async Task GetBooks_WhenUpstreamGoesDown_AfterCacheIsWarmed_ReturnsCachedData()
    {
        using var client = CreateFreshClient();

        // Step 1: warm the cache with good data
        var warmResponse = await client.GetAsync("/api/v1/books");
        warmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 2: upstream goes down
        _wireMock.Reset();
        _wireMock
            .Given(Request.Create().WithPath("/api/v1/bookowners").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        // Step 3: same client = same cache — stale data is served
        var downResponse = await client.GetAsync("/api/v1/books");
        downResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await downResponse.Content.ReadFromJsonAsync<GetBooksResponse>();
        body!.Groups.Should().HaveCount(2);
    }

    // Health check

    [Test]
    public async Task HealthCheck_Returns200()
    {
        using var client = CreateFreshClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Helpers

    /// <summary>
    /// Creates a client backed by a new WebApplicationFactory with a fresh empty IMemoryCache.
    /// Guarantees no cache state leaks between tests.
    /// </summary>
    private HttpClient CreateFreshClient()
    {
        var freshCache = new MemoryCache(new MemoryCacheOptions());

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["BupaApi:BaseUrl"] = _wireMock.Url!
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Replace IMemoryCache with a fresh instance — isolates cache per test
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IMemoryCache));
                    if (descriptor is not null)
                        services.Remove(descriptor);

                    services.AddSingleton<IMemoryCache>(freshCache);
                });
            });

        return factory.CreateClient();
    }

    private void StubUpstreamWithFullData()
    {
        _wireMock
            .Given(Request.Create().WithPath("/api/v1/bookowners").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(BookOwnersJson));
    }
}