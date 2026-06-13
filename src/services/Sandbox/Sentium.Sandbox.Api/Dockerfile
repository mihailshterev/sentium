# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/services/Sandbox/Sentium.Sandbox.Api/Sentium.Sandbox.Api.csproj \
    -c Release -o /app /p:UseAppHost=false /p:TreatWarningsAsErrors=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Sentium.Sandbox.Api.dll"]
