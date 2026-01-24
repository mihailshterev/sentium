# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all IdentityProvider projects (for project references)
COPY services/IdentityProvider ./services/IdentityProvider

# Restore
RUN dotnet restore services/IdentityProvider/IdentityProvider.Api/IdentityProvider.Api.csproj

# Publish
WORKDIR /src/services/IdentityProvider/IdentityProvider.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "IdentityProvider.Api.dll"]
