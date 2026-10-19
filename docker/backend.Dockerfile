# syntax=docker/dockerfile:1
# TonTuyau backend: the API and the `tontuyau` command line (QA13-6). Built from the repository's root:
#   docker build -f docker/backend.Dockerfile -t ghcr.io/tontuyau/tontuyau-backend:dev .
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
COPY backend/src/TonTuyau.YouTube/TonTuyau.YouTube.csproj backend/src/TonTuyau.YouTube/
WORKDIR /src/backend
RUN dotnet restore src/TonTuyau.Api/TonTuyau.Api.csproj --runtime linux-x64
COPY backend/src/ src/
# linux-x64 only: just that platform's native libraries (SkiaSharp's libSkiaSharp.so), framework-dependent.
RUN dotnet publish src/TonTuyau.Api/TonTuyau.Api.csproj \
      --configuration Release --runtime linux-x64 --self-contained false --no-restore --output /app \
 && rm -f /app/appsettings.Development.json

# yt-dlp and Deno (1.5: TonTuyau reads YouTube with yt-dlp, which runs YouTube's JavaScript with Deno), pinned: each
# version with the SHA-256 its GitHub release publishes, checked before use. YouTube changes often and yt-dlp follows:
# a new yt-dlp is a new TonTuyau release. ffmpeg comes with the downloads (1.6).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS tools
ARG YTDLP_VERSION=2026.08.19
ARG YTDLP_SHA256=58162f9bfdc27458ea47bfcb311cf47028f17d8154a8bf7d689861d46399230a
ARG DENO_VERSION=2.9.7
ARG DENO_SHA256=c6527f24f4b16031d3ae4fa9f658d5f11534c8d84ce7dc8502420280919c3490
RUN apt-get update \
 && apt-get install --yes --no-install-recommends unzip \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /tools
RUN curl --fail --silent --show-error --location --output yt-dlp \
      "https://github.com/yt-dlp/yt-dlp/releases/download/${YTDLP_VERSION}/yt-dlp_linux" \
 && echo "${YTDLP_SHA256}  yt-dlp" | sha256sum --check - \
 && curl --fail --silent --show-error --location --output deno.zip \
      "https://github.com/denoland/deno/releases/download/v${DENO_VERSION}/deno-x86_64-unknown-linux-gnu.zip" \
 && echo "${DENO_SHA256}  deno.zip" | sha256sum --check - \
 && unzip -q deno.zip deno \
 && rm deno.zip

# The data folders, owned by the image's non-root user (app, uid 1654): made here, since the final image has no shell.
# Volumes mounted there start with this ownership. The media share is mounted read-only at /youtube.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS folders
RUN mkdir -p /out/data/art /out/data/tools /out/youtube

# Ubuntu "chiseled" (1.5.1): only what .NET needs (glibc, libstdc++, OpenSSL, ICU, time zones), no shell, no package
# manager, non-root. Deno needs nothing more (glibc, libgcc); yt-dlp also zlib (below). CI runs both in the image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
ARG VERSION=dev
ARG REVISION=unknown
LABEL org.opencontainers.image.title="TonTuyau backend" \
      org.opencontainers.image.description="TonTuyau's API and sync (a read-only, YouTube-like UI over a TubeArchivist library)" \
      org.opencontainers.image.source="https://github.com/TonTuyau/TonTuyau" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}"
COPY --from=folders --chown=1654:1654 /out/data /data
COPY --from=folders /out/youtube /youtube
COPY --from=build /app /app
COPY --from=tools --chmod=755 /tools/yt-dlp /tools/deno /usr/local/bin/
# yt-dlp's packed Python opens zlib, which the chiseled image doesn't carry: the same Ubuntu 24.04 library, alone.
COPY --from=tools /usr/lib/x86_64-linux-gnu/libz.so.1 /usr/lib/x86_64-linux-gnu/libz.so.1
# `tontuyau` on the PATH: `docker exec tontuyau-backend tontuyau status`. Deno's cache in /tmp: the app user's home
# (/app) isn't writable.
ENV PATH="/app:${PATH}" \
    ASPNETCORE_HTTP_PORTS=8080 \
    TONTUYAU_MEDIA_ROOT=/youtube \
    TONTUYAU_ART_ROOT=/data/art \
    TONTUYAU_TOOLS_ROOT=/data/tools \
    DENO_DIR=/tmp/deno
USER 1654
WORKDIR /app
EXPOSE 8080
ENTRYPOINT ["/app/tontuyau"]
