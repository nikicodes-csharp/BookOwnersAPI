using System.Net;
using System.Net.Http.Json;
using BookOwners.Application.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace BookOwners.IntegrationTests;

/// <summary>
/// Spins up a real ASP.NET Core test server and uses WireMock to stub the upstream Bupa API.
/// This validates the full pipeline: HTTP client → repository → grouping → controller → JSON response.
/// </summary>
[TestFixture]
public sealed class BooksControllerIntegrationTests
{
    private WireMockServer _wireMock = null!;
    private HttpClient _client = null!;
    private WebApplicationFactory<Program> _factory = null!;

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

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["BupaApi:BaseUrl"] = _wireMock.Url!
                    });
                });
            });

        _client = _factory.CreateClient();
    }

    [SetUp]
    public void SetUp()
    {
        _wireMock.Reset();
        _wireMock
            .Given(Request.Create().WithPath("/api/v1/bookowners").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(BookOwnersJson));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
        _wireMock.Stop();
    }

    [Test]
    public async Task GetBooks_NoFilter_Returns200WithTwoGroups()
    {
        var response = await _client.GetAsync("/api/v1/books");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();
        body.Should().NotBeNull();
        body!.Groups.Should().HaveCount(2);
        body.Groups.Should().Contain(g => g.Category == "Adults");
        body.Groups.Should().Contain(g => g.Category == "Children");
    }

    [Test]
    public async Task GetBooks_NoFilter_AdultBooksAreSortedAlphabetically()
    {
        var response = await _client.GetAsync("/api/v1/books");
        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();

        var adultBooks = body!.Groups.Single(g => g.Category == "Adults").Books;
        adultBooks.Select(b => b.Name).Should().BeInAscendingOrder();
    }

    [Test]
    public async Task GetBooks_NoFilter_ChildrenBooksAreSortedAlphabetically()
    {
        var response = await _client.GetAsync("/api/v1/books");
        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();

        var childBooks = body!.Groups.Single(g => g.Category == "Children").Books;
        childBooks.Select(b => b.Name).Should().BeInAscendingOrder();
    }

    [Test]
    public async Task GetBooks_HardcoverOnly_ReturnsOnlyHardcoverBooks()
    {
        var response = await _client.GetAsync("/api/v1/books?hardcoverOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();
        var allBooks = body!.Groups.SelectMany(g => g.Books);
        allBooks.Should().AllSatisfy(b => b.Type.Should().Be("Hardcover"));
    }

    [Test]
    public async Task GetBooks_HardcoverOnly_ExcludesEbookAndPaperback()
    {
        var response = await _client.GetAsync("/api/v1/books?hardcoverOnly=true");
        var body = await response.Content.ReadFromJsonAsync<GetBooksResponse>();

        var allBooks = body!.Groups.SelectMany(g => g.Books).ToList();
        allBooks.Should().NotContain(b => b.Name == "The Hobbit");       // Ebook
        allBooks.Should().NotContain(b => b.Name == "Wuthering Heights"); // Paperback
        allBooks.Should().NotContain(b => b.Name == "Jane Eyre");         // Paperback
        allBooks.Should().NotContain(b => b.Name == "Hamlet" && b.Type == "Paperback");
    }

    [Test]
    public async Task GetBooks_WhenUpstreamApiIsDown_Returns502()
    {
        _wireMock.Reset();
        _wireMock
            .Given(Request.Create().WithPath("/api/v1/bookowners").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        var response = await _client.GetAsync("/api/v1/books");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Test]
    public async Task GetBooks_HealthCheck_Returns200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}