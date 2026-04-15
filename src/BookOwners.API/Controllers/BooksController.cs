using BookOwners.Application.DTOs;
using BookOwners.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BookOwners.API.Controllers;

[ApiController]
[Route("api/v1/books")]
[Produces("application/json")]
public sealed class BooksController : ControllerBase
{
    private readonly IBookService _bookService;

    public BooksController(IBookService bookService) => _bookService = bookService;

    /// <summary>
    /// Returns all books grouped by owner age category (Adults / Children), sorted alphabetically.
    /// </summary>
    /// <param name="hardcoverOnly">When true, only Hardcover books are returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(GetBooksResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GetBooksResponse>> GetBooks(
        [FromQuery] bool hardcoverOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _bookService.GetBooksAsync(hardcoverOnly, cancellationToken);
        return Ok(result);
    }
}