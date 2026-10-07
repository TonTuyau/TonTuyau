# syntax=docker/dockerfile:1
# TonTuyau backend: the API and the `tontuyau` command line (QA13-6). Built from the repository's root:
#   docker build -f docker/backend.Dockerfile -t ghcr.io/eclegv/tontuyau-backend:dev .
# Only backend/src and the build settings are copied (.dockerignore keeps .env, dev-data/, bin/ and obj/ out).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore first, from the project files alone: this layer is reused until a dependency changes.
COPY .editorconfig ./
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/
COPY backend/src/TonTuyau.Api/TonTuyau.Api.csproj backend/src/TonTuyau.Api/
COPY backend/src/TonTuyau.Application/TonTuyau.Application.csproj backend/src/TonTuyau.Application/
COPY backend/src/TonTuyau.Infrastructure/TonTuyau.Infrastructure.csproj backend/src/TonTuyau.Infrastructure/
COPY backend/src/TonTuyau.TubeArchivist/TonTuyau.TubeArchivist.csproj backend/src/TonTuyau.TubeArchivist/
WORKDIR /src/backend
RUN dotnet restore src/TonTuyau.Api/TonTuyau.Api.csproj --runtime linux-x64
COPY backend/src/ src/
# linux-x64 only: just that platform's native libraries (SkiaSharp's libSkiaSharp.so), framework-dependent.
RUN dotnet publish src/TonTuyau.Api/TonTuyau.Api.csproj \
      --configuration Release --runtime linux-x64 --self-contained false --no-restore --output /app \
 && rm -f /app/appsettings.Development.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG VERSION=dev
ARG REVISION=unknown
LABEL org.opencontainers.image.title="TonTuyau backend" \
      org.opencontainers.image.description="TonTuyau's API and sync (a read-only, YouTube-like UI over a TubeArchivist library)" \
      org.opencontainers.image.source="https://github.com/Eclegv/TonTuyau" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}"
# The image's own non-root user (app, uid 1654) owns the art and the sync logs; volumes mounted there start with
# this ownership. The media share is mounted read-only at /youtube.
RUN mkdir -p /data/art /data/logs/sync /youtube \
 && chown -R "$APP_UID:$APP_UID" /data
COPY --from=build /app /app
# `tontuyau` on the PATH: `docker exec tontuyau-backend tontuyau sync status`.
ENV PATH="/app:${PATH}" \
    ASPNETCORE_HTTP_PORTS=8080 \
    TONTUYAU_MEDIA_ROOT=/youtube \
    TONTUYAU_ART_ROOT=/data/art \
    TONTUYAU_SYNC_LOG_ROOT=/data/logs/sync
USER $APP_UID
WORKDIR /app
EXPOSE 8080
ENTRYPOINT ["/app/tontuyau"]
