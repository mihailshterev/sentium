# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files
COPY *.sln .
COPY services/identity-provider/IdentityProvider.Api/*.csproj ./IdentityProvider.Api/

RUN dotnet restore ./IdentityProvider.Api/IdentityProvider.Api.csproj

# Copy source code
COPY services/ ./services/

# Publish
WORKDIR /src/services/identity-provider/IdentityProvider.Api
RUN dotnet publish -c Release -o /app/publish

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "IdentityProvider.Api.dll"]
