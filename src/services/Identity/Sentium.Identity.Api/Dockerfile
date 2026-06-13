# syntax=docker/dockerfile:1
FROM node:22-alpine AS ui
WORKDIR /ui
RUN npm install -g pnpm@11.0.6
COPY src/clients/sentium-identity-ui/package.json src/clients/sentium-identity-ui/pnpm-lock.yaml src/clients/sentium-identity-ui/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY src/clients/sentium-identity-ui/ ./
RUN pnpm exec vite build --outDir dist --emptyOutDir

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
COPY --from=ui /ui/dist ./src/services/Identity/Sentium.Identity.Api/wwwroot
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/services/Identity/Sentium.Identity.Api/Sentium.Identity.Api.csproj \
    -c Release -o /app /p:UseAppHost=false /p:TreatWarningsAsErrors=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Sentium.Identity.Api.dll"]
