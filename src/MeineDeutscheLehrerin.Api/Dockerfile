# Build context is the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore using only the project files first (better layer caching).
COPY src/MeineDeutscheLehrerin.Domain/*.csproj src/MeineDeutscheLehrerin.Domain/
COPY src/MeineDeutscheLehrerin.Infrastructure/*.csproj src/MeineDeutscheLehrerin.Infrastructure/
COPY src/MeineDeutscheLehrerin.Api/*.csproj src/MeineDeutscheLehrerin.Api/
RUN dotnet restore src/MeineDeutscheLehrerin.Api/MeineDeutscheLehrerin.Api.csproj

COPY src/ src/
RUN dotnet publish src/MeineDeutscheLehrerin.Api/MeineDeutscheLehrerin.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
# Curated JSON decks, imported on startup when Seed:ImportContent is enabled.
COPY content/ ./content/
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MeineDeutscheLehrerin.Api.dll"]
