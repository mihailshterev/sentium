# syntax=docker/dockerfile:1
# Portal: built into src/hosts/Sentium.Web/wwwroot (vite's outDir), served by Sentium.Web itself.
FROM node:22-alpine AS portal
RUN npm install -g pnpm@12.9.1
WORKDIR /src
# The lockfile covers the whole pnpm workspace, so every member's manifest must be present for a frozen install.
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml ./
COPY src/clients/sentium-portal/package.json src/clients/sentium-portal/
COPY src/clients/sentium-satelles/package.json src/clients/sentium-satelles/
COPY e2e/package.json e2e/
RUN pnpm install --frozen-lockfile --filter sentium-portal
COPY src/clients/sentium-portal/ src/clients/sentium-portal/
RUN pnpm --filter sentium-portal build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
COPY --from=portal /src/src/hosts/Sentium.Web/wwwroot src/hosts/Sentium.Web/wwwroot
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/hosts/Sentium.Web/Sentium.Web.csproj \
    -c Release -o /app /p:UseAppHost=false /p:TreatWarningsAsErrors=false /p:RestoreLockedMode=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Data Protection key ring mount point (a named volume copies this directory's ownership on first use).
RUN mkdir -p /keys && chown "$APP_UID" /keys
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# The image's unprivileged "app" user: the web process needs no root, only write access to /keys (above).
USER $APP_UID

ENTRYPOINT ["dotnet", "Sentium.Web.dll"]
