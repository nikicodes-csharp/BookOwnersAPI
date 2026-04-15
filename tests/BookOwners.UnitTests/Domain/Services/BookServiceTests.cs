using BookOwners.Application.Interfaces;
using BookOwners.Application.Services;
using BookOwners.Domain.Entities;
using BookOwners.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace BookOwners.UnitTests.Services;

[TestFixture]
public sealed class BookServiceTests
{
    private IBookOwnerRepository _repository = null!;
    private BookService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IBookOwnerRepository>();
        _sut = new BookService(_repository, new BookGroupingService());
    }

    [Test]
    public async Task GetBooksAsync_NoFilter_ReturnsAllBooksGrouped()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new BookOwner("Jane", 23, [new Book("Hamlet", BookType.Hardcover)]),
            new BookOwner("Charles", 17, [new Book("The Hobbit", BookType.Ebook)])
        ]);

        var result = await _sut.GetBooksAsync();

        result.Groups.Should().HaveCount(2);
        result.Groups.Should().Contain(g => g.Category == "Adults");
        result.Groups.Should().Contain(g => g.Category == "Children");
    }

    [Test]
    public async Task GetBooksAsync_HardcoverOnly_FiltersOtherTypes()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new BookOwner("Jane", 23, [
                new Book("Hamlet", BookType.Hardcover),
                new Book("Wuthering Heights", BookType.Paperback)
            ])
        ]);

        var result = await _sut.GetBooksAsync(hardcoverOnly: true);

        var adultBooks = result.Groups.Single(g => g.Category == "Adults").Books;
        adultBooks.Should().ContainSingle(b => b.Name == "Hamlet");
        adultBooks.Should().NotContain(b => b.Name == "Wuthering Heights");
    }

    [Test]
    public async Task GetBooksAsync_CallsRepositoryExactlyOnce()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        await _sut.GetBooksAsync();
        await _repository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetBooksAsync_BooksInGroupAreSortedAlphabetically()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new BookOwner("Jane", 23, [
                new Book("Zebra", BookType.Hardcover),
                new Book("Apple", BookType.Hardcover)
            ])
        ]);

        var result = await _sut.GetBooksAsync();

        var books = result.Groups.Single(g => g.Category == "Adults").Books;
        books[0].Name.Should().Be("Apple");
        books[1].Name.Should().Be("Zebra");
    }

    [Test]
    public async Task GetBooksAsync_EmptyRepository_ReturnsEmptyGroups()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        var result = await _sut.GetBooksAsync();
        result.Groups.Should().BeEmpty();
    }

    [Test]
    public async Task GetBooksAsync_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        await _sut.GetBooksAsync(cancellationToken: cts.Token);
        await _repository.Received(1).GetAllAsync(cts.Token);
    }
}