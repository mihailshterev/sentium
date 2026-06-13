# syntax=docker/dockerfile:1
FROM node:22-alpine AS build
WORKDIR /app
RUN npm install -g pnpm@11.0.6
COPY src/clients/sentium-portal/package.json src/clients/sentium-portal/pnpm-lock.yaml src/clients/sentium-portal/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY src/clients/sentium-portal/ ./
ARG VITE_API_BASE=https://localhost:8443
ENV VITE_API_BASE=$VITE_API_BASE
RUN pnpm build

FROM nginx:stable AS final
COPY src/clients/sentium-portal/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
