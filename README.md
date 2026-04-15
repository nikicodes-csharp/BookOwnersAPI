# BookOwners API

Web API that fetches book owner data from the Bupa upstream API and returns books grouped by owner age category, sorted alphabetically.

---

## The Problem

Given a list of book owners with their ages and books, return all books:
- Grouped under **Adults** (age 18 and above) or **Children** (age 17 and below)
- Sorted **alphabetically** within each group
- Optionally filtered to **Hardcover** books only

Data is sourced from: `https://digitalcodingtest.bupa.com.au/api/v1/bookowners`

---

## Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Or [Docker](https://www.docker.com/products/docker-desktop)

### Run locally
```bash
dotnet run --project src/BookOwners.API
```

Open Swagger UI at: `http://localhost:5000`

### Run with Docker
```bash
docker compose up --build
```

API available at: `http://localhost:8080`

### Run tests
```bash
dotnet test
```

---

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/books` | All books grouped by age category, sorted A-Z |
| `GET` | `/api/v1/books?hardcoverOnly=true` | Hardcover books only |
| `GET` | `/health` | Health check |
| `GET` | `/` | Swagger UI (Development only) |

### Example — `GET /api/v1/books`

```json
{
  "groups": [
    {
      "category": "Adults",
      "books": [
        { "name": "Great Expectations",       "type": "Hardcover" },
        { "name": "Gulliver's Travels",        "type": "Hardcover" },
        { "name": "Hamlet",                    "type": "Hardcover" },
        { "name": "Jane Eyre",                 "type": "Paperback" },
        { "name": "React: The Ultimate Guide", "type": "Hardcover" },
        { "name": "Wuthering Heights",         "type": "Paperback" }
      ]
    },
    {
      "category": "Children",
      "books": [
        { "name": "Great Expectations",     "type": "Hardcover" },
        { "name": "Hamlet",                 "type": "Paperback" },
        { "name": "Little Red Riding Hood", "type": "Hardcover" },
        { "name": "The Hobbit",             "type": "Ebook"     }
      ]
    }
  ]
}
```

### Example — `GET /api/v1/books?hardcoverOnly=true`

```json
{
  "groups": [
    {
      "category": "Adults",
      "books": [
        { "name": "Great Expectations",       "type": "Hardcover" },
        { "name": "Gulliver's Travels",        "type": "Hardcover" },
        { "name": "Hamlet",                    "type": "Hardcover" },
        { "name": "React: The Ultimate Guide", "type": "Hardcover" }
      ]
    },
    {
      "category": "Children",
      "books": [
        { "name": "Great Expectations",     "type": "Hardcover" },
        { "name": "Little Red Riding Hood", "type": "Hardcover" }
      ]
    }
  ]
}
```

---

## Project Structure

```
BookOwners/
├── Directory.Build.props            # Shared build settings across all projects
├── Directory.Packages.props         # Centralised NuGet package versions (CPM)
├── Dockerfile                       # Multi-stage: restore → build → test → publish → runtime
├── docker-compose.yml
│
├── src/
│   ├── BookOwners.Domain/           # Pure domain layer — zero dependencies
│   │   ├── Entities/                # Book, BookOwner, AgeCategoryGrouping
│   │   └── Enums/                   # AgeCategory, BookType
│   │
│   ├── BookOwners.Application/      # Business logic — interfaces, services, DTOs
│   │   ├── Interfaces/              # IBookService, IBookGroupingService, IBookOwnerRepository
│   │   ├── Services/                # BookService, BookGroupingService
│   │   └── DTOs/                    # GetBooksResponse, AgeCategoryGroupingDto, BookDto
│   │
│   ├── BookOwners.Infrastructure/   # External concerns — HTTP client, mapping
│   │   ├── Http/                    # BookOwnerApiResponse (raw API shape)
│   │   └── Services/                # BookOwnerRepository (HTTP + retry + cache)
│   │
│   └── BookOwners.API/              # ASP.NET Core host
│       ├── Controllers/             # BooksController
│       ├── Middleware/              # GlobalExceptionHandlerMiddleware
│       ├── Program.cs
│       └── appsettings.json
│
└── tests/
    ├── BookOwners.UnitTests/        # NUnit + NSubstitute + FluentAssertions
    │   ├── Domain/                  # BookOwnerTests
    │   └── Services/                # BookServiceTests, BookGroupingServiceTests
    │
    └── BookOwners.IntegrationTests/ # NUnit + WireMock.Net + WebApplicationFactory
        └── BooksControllerIntegrationTests
```

---

## Architecture & Design Decisions

### Clean Architecture

Dependencies only point inward. Outer layers depend on inner layers, never the reverse:

```
API  ──────────────────────→  Application  →  Domain
Infrastructure  ────────────→  Application  →  Domain
```

The Domain and Application layers have zero knowledge of ASP.NET Core, HTTP, or JSON. They can be unit tested with no infrastructure at all.

## Configuration

| Key | Default | Description |
|-----|---------|-------------|
| `BupaApi:BaseUrl` | `https://digitalcodingtest.bupa.com.au/` | Upstream API base URL |

Override in Docker via environment variable (double underscore for section nesting):
```
BupaApi__BaseUrl=https://digitalcodingtest.bupa.com.au/
```

---

## Testing Strategy

### Unit Tests

Tests business logic in complete isolation --> no network calls, no ASP.NET Core.

| Test class | Coverage |
|------------|----------|
| `BookOwnerTests` | Age boundary: exactly 18 = Adult, exactly 17 = Child |
| `BookGroupingServiceTests` | Alphabetical sorting, hardcover filter, mixed categories, edge cases, full JSON data scenario |
| `BookServiceTests` | Orchestration, filter passthrough, cancellation token forwarding, repository call count |

Tools: **NUnit** · **NSubstitute** · **FluentAssertions**

### Integration Tests

Spins up a real ASP.NET Core test server. Stubs the upstream Bupa API with WireMock so no real network calls are made.

Tests the full pipeline: HTTP request → controller → service → repository → HTTP client → WireMock stub → JSON response.

| Test | Coverage |
|------|----------|
| All books returns 200 with two groups | Happy path |
| Adults and Children books sorted alphabetically | Sort order |
| `hardcoverOnly=true` excludes Paperback and Ebook | Filter correctness |
| Upstream API returns 503 → our API returns 502 | Error handling |
| `/health` returns 200 | Health check |

Tools: **NUnit** · **WireMock.Net** · **WebApplicationFactory** · **FluentAssertions**

---

## Security Considerations

- **Non-root Docker user** --> container runs as `appuser`, not root (principle of least privilege)
- **No secrets in source** --> `BupaApi:BaseUrl` is config; sensitive values go in environment variables
- **Stack traces never exposed** --> `GlobalExceptionHandlerMiddleware` returns a clean JSON error body for all exceptions
- **Nullable reference types** --> `<Nullable>enable</Nullable>` catches null dereference at compile time
- **Warnings as errors** --> `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` prevents warning accumulation
  - **HttpClient timeout** --> 30s timeout prevents thread exhaustion if upstream hangs
- **CORS configured** — explicit policy in `Program.cs` controls cross-origin access

---

## CI/CD

GitHub Actions (`.github/workflows/ci.yml`) runs on every push to `main` or `develop` and on all pull requests:

```
dotnet restore → dotnet build → unit tests → integration tests → docker image build
```

---

## Assumptions

1. **Duplicate titles are not deduplicated** --> if two adults both own "Hamlet", it appears twice in the Adults group (one entry per owner copy).
2. **Unknown book types default to Paperback** --> if the upstream API returns an unrecognised `type` value, it is treated as Paperback.
3. **Age boundary is inclusive at 18** --> exactly age 18 = Adult; exactly age 17 = Child, per the spec ("18 and above" / "17 and below").
4. **Empty groups are omitted** --> if a filter leaves a category with no books, that category does not appear in the response.
5. **Sorting is case-insensitive** --> "the Hobbit" sorts identically to "The Hobbit".
6. **Upstream API is read-only** --> no write operations are made to `digitalcodingtest.bupa.com.au`.
7. **No authentication on upstream API** --> the Bupa coding test endpoint is open. If auth were added it would be handled via a delegating handler on the `HttpClient`.
8. **In-memory cache used** --> in a production multi-instance deployment this would be replaced with a distributed cache (e.g. Redis) with a TTL.