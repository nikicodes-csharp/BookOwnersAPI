using BookOwners.Domain.Entities;
using BookOwners.Domain.Enums;
using FluentAssertions;
using NUnit.Framework;

namespace BookOwners.UnitTests.Domain;

[TestFixture]
public sealed class BookOwnerTests
{
    [TestCase(18, AgeCategory.Adults)]
    [TestCase(19, AgeCategory.Adults)]
    [TestCase(100, AgeCategory.Adults)]
    [TestCase(17, AgeCategory.Children)]
    [TestCase(0, AgeCategory.Children)]
    public void AgeCategory_ReturnsCorrectCategory(int age, AgeCategory expected)
    {
        var owner = new BookOwner("Test", age, []);
        owner.AgeCategory.Should().Be(expected);
    }
}