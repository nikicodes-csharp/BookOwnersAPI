using BookOwners.Application.Services;
using BookOwners.Domain.Entities;
using BookOwners.Domain.Enums;
using FluentAssertions;
using NUnit.Framework;

namespace BookOwners.UnitTests.Services;

[TestFixture]
public sealed class BookGroupingServiceTests
{
    private BookGroupingService _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new BookGroupingService();

    // Adults vs Children categorisation

    [Test]
    public void GroupByAgeCategory_OwnerAgedExactly18_IsClassifiedAsAdult()
    {
        var owners = new List<BookOwner>
        {
            new("Alice", 18, [new Book("Hamlet", BookType.Paperback)])
        };

        var result = _sut.GroupByAgeCategory(owners);

        result.Should().ContainSingle(g => g.Category == AgeCategory.Adults);
        result.Should().NotContain(g => g.Category == AgeCategory.Children);
    }

    [Test]
    public void GroupByAgeCategory_OwnerAgedExactly17_IsClassifiedAsChild()
    {
        var owners = new List<BookOwner>
        {
            new("Bob", 17, [new Book("Hamlet", BookType.Paperback)])
        };

        var result = _sut.GroupByAgeCategory(owners);

        result.Should().ContainSingle(g => g.Category == AgeCategory.Children);
        result.Should().NotContain(g => g.Category == AgeCategory.Adults);
    }

    // Alphabetical sorting

    [Test]
    public void GroupByAgeCategory_BooksWithinGroup_AreSortedAlphabetically()
    {
        var owners = new List<BookOwner>
        {
            new("Jane", 23, [
                new Book("Wuthering Heights", BookType.Paperback),
                new Book("Hamlet", BookType.Hardcover)
            ])
        };

        var result = _sut.GroupByAgeCategory(owners);

        var adultBooks = result.Single(g => g.Category == AgeCategory.Adults).Books;
        adultBooks.Select(b => b.Name).Should().BeInAscendingOrder();
    }

    [Test]
    public void GroupByAgeCategory_BooksAreSortedCaseInsensitively()
    {
        var owners = new List<BookOwner>
        {
            new("Jane", 23, [
                new Book("zebra Tales", BookType.Paperback),
                new Book("Apple Stories", BookType.Hardcover)
            ])
        };

        var result = _sut.GroupByAgeCategory(owners);

        var books = result.Single(g => g.Category == AgeCategory.Adults).Books;
        books[0].Name.Should().Be("Apple Stories");
        books[1].Name.Should().Be("zebra Tales");
    }

    // Hardcover filter

    [Test]
    public void GroupByAgeCategory_HardcoverFilter_ExcludesPaperbackAndEbook()
    {
        var owners = new List<BookOwner>
        {
            new("Max", 25, [
                new Book("React Guide", BookType.Hardcover),
                new Book("Jane Eyre", BookType.Paperback),
                new Book("The Hobbit", BookType.Ebook)
            ])
        };

        var result = _sut.GroupByAgeCategory(owners, filterByType: BookType.Hardcover);

        var books = result.Single(g => g.Category == AgeCategory.Adults).Books;
        books.Should().ContainSingle(b => b.Name == "React Guide");
        books.Should().NotContain(b => b.Name == "Jane Eyre");
        books.Should().NotContain(b => b.Name == "The Hobbit");
    }

    [Test]
    public void GroupByAgeCategory_HardcoverFilter_WhenNoHardcoversExist_ReturnsEmptyGroups()
    {
        var owners = new List<BookOwner>
        {
            new("Jane", 23, [new Book("Hamlet", BookType.Paperback)])
        };

        var result = _sut.GroupByAgeCategory(owners, filterByType: BookType.Hardcover);

        result.Should().BeEmpty();
    }

    // Multiple owners, deduplication of book names

    [Test]
    public void GroupByAgeCategory_MultipleOwnersInSameCategory_AllBooksIncluded()
    {
        var owners = new List<BookOwner>
        {
            new("Jane", 23, [new Book("Hamlet", BookType.Hardcover)]),
            new("Max", 25, [new Book("Great Expectations", BookType.Hardcover)])
        };

        var result = _sut.GroupByAgeCategory(owners);

        var books = result.Single(g => g.Category == AgeCategory.Adults).Books;
        books.Should().HaveCount(2);
    }

    [Test]
    public void GroupByAgeCategory_SameBookOwnedByMultiplePeople_AppearsTwice()
    {
        // Assumption: duplicate book titles are NOT deduplicated, each owner's copy is listed
        var owners = new List<BookOwner>
        {
            new("Jane", 23, [new Book("Hamlet", BookType.Hardcover)]),
            new("Max", 25, [new Book("Hamlet", BookType.Paperback)])
        };

        var result = _sut.GroupByAgeCategory(owners);

        var books = result.Single(g => g.Category == AgeCategory.Adults).Books;
        books.Where(b => b.Name == "Hamlet").Should().HaveCount(2);
    }

    // Empty input

    [Test]
    public void GroupByAgeCategory_EmptyOwnerList_ReturnsEmpty()
    {
        var result = _sut.GroupByAgeCategory([]);
        result.Should().BeEmpty();
    }

    // Full data scenario matching the provided JSON

    [Test]
    public void GroupByAgeCategory_WithProvidedJsonData_ReturnsCorrectGroupings()
    {
        var owners = BuildProvidedJsonOwners();

        var result = _sut.GroupByAgeCategory(owners);

        var childGroup = result.Single(g => g.Category == AgeCategory.Children);
        var adultGroup = result.Single(g => g.Category == AgeCategory.Adults);

        // Charlotte (14), William (15), Charles (17) → Children
        childGroup.Books.Select(b => b.Name).Should().BeInAscendingOrder();
        childGroup.Books.Select(b => b.Name).Should().Contain("Great Expectations");
        childGroup.Books.Select(b => b.Name).Should().Contain("Hamlet");
        childGroup.Books.Select(b => b.Name).Should().Contain("Little Red Riding Hood");
        childGroup.Books.Select(b => b.Name).Should().Contain("The Hobbit");

        // Jane (23), Max (25) → Adults
        adultGroup.Books.Select(b => b.Name).Should().BeInAscendingOrder();
        adultGroup.Books.Select(b => b.Name).Should().Contain("Great Expectations");
        adultGroup.Books.Select(b => b.Name).Should().Contain("Hamlet");
    }

    [Test]
    public void GroupByAgeCategory_WithProvidedJsonData_HardcoverOnly_FiltersCorrectly()
    {
        var owners = BuildProvidedJsonOwners();

        var result = _sut.GroupByAgeCategory(owners, filterByType: BookType.Hardcover);

        foreach (var group in result)
        {
            group.Books.Should().AllSatisfy(b => b.Type.Should().Be(BookType.Hardcover));
        }

        // Charles's Ebook (The Hobbit) and Paperbacks must be excluded
        var childGroup = result.Single(g => g.Category == AgeCategory.Children);
        childGroup.Books.Should().NotContain(b => b.Name == "The Hobbit");
    }

    // Helper

    private static List<BookOwner> BuildProvidedJsonOwners() =>
    [
        new("Jane", 23, [
            new Book("Hamlet", BookType.Hardcover),
            new Book("Wuthering Heights", BookType.Paperback)
        ]),
        new("Charlotte", 14, [
            new Book("Hamlet", BookType.Paperback)
        ]),
        new("Max", 25, [
            new Book("React: The Ultimate Guide", BookType.Hardcover),
            new Book("Gulliver's Travels", BookType.Hardcover),
            new Book("Jane Eyre", BookType.Paperback),
            new Book("Great Expectations", BookType.Hardcover)
        ]),
        new("William", 15, [
            new Book("Great Expectations", BookType.Hardcover)
        ]),
        new("Charles", 17, [
            new Book("Little Red Riding Hood", BookType.Hardcover),
            new Book("The Hobbit", BookType.Ebook)
        ])
    ];
}