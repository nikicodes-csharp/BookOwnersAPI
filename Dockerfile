FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BookOwners.sln .
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/BookOwners.Domain/BookOwners.Domain.csproj             src/BookOwners.Domain/
COPY src/BookOwners.Application/BookOwners.Application.csproj   src/BookOwners.Application/
COPY src/BookOwners.Infrastructure/BookOwners.Infrastructure.csproj src/BookOwners.Infrastructure/
COPY src/BookOwners.API/BookOwners.API.csproj                   src/BookOwners.API/
COPY tests/BookOwners.UnitTests/BookOwners.UnitTests.csproj     tests/BookOwners.UnitTests/
COPY tests/BookOwners.IntegrationTests/BookOwners.IntegrationTests.csproj tests/BookOwners.IntegrationTests/

RUN dotnet restore
COPY . .
RUN dotnet build --no-restore -c Release

FROM build AS test
RUN dotnet test --no-build -c Release \
    --logger "console;verbosity=normal" \
    --collect:"XPlat Code Coverage"

FROM build AS publish
RUN dotnet publish src/BookOwners.API/BookOwners.API.csproj \
    --no-build -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "BookOwners.API.dll"]