# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/hosts/Sentium.Backup/Sentium.Backup.csproj \
    -c Release -o /app /p:UseAppHost=false /p:TreatWarningsAsErrors=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# The database server's own image, so pg_dump, pg_restore and psql match the server version exactly.
FROM pgvector/pgvector:pg18-trixie AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends age ca-certificates libicu76 tzdata \
    && rm -rf /var/lib/apt/lists/*
COPY --from=runtime /usr/share/dotnet /usr/share/dotnet
RUN ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_NOLOGO=1
EXPOSE 8080

# Runs as root: it reads the Sentium.Web key volume and writes to an operator-chosen destination whose owner
# varies (NAS mounts). Compose drops every capability it does not need.
ENTRYPOINT ["dotnet", "/app/Sentium.Backup.dll"]
