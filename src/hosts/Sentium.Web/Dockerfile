# syntax=docker/dockerfile:1
# Portal: built into src/hosts/Sentium.Web/wwwroot (vite's outDir), served by Sentium.Web itself.
FROM node:22-alpine AS portal
RUN npm install -g pnpm@12.9.1
WORKDIR /src/src/clients/sentium-portal
COPY src/clients/sentium-portal/package.json src/clients/sentium-portal/pnpm-lock.yaml src/clients/sentium-portal/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY src/clients/sentium-portal/ ./
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
COPY --from=portal /src/src/hosts/Sentium.Web/wwwroot src/hosts/Sentium.Web/wwwroot
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/hosts/Sentium.Web/Sentium.Web.csproj \
    -c Release -o /app /p:UseAppHost=false /p:TreatWarningsAsErrors=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Data Protection key ring mount point (a named volume copies this directory's ownership on first use).
RUN mkdir -p /keys && chown "$APP_UID" /keys
COPY --from=build /app .
ENV ASPNETCORE_URLS=https://+:8443;http://+:8080
EXPOSE 8080
EXPOSE 8443

ENTRYPOINT ["dotnet", "Sentium.Web.dll"]
