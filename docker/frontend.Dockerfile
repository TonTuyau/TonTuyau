# syntax=docker/dockerfile:1
# TonTuyau front: the built app and the small Node server that serves it and forwards /api, /media, /art to the
# backend (QA13-6, QA13-9). Built from the repository's root:
#   docker build -f docker/frontend.Dockerfile -t ghcr.io/tontuyau/tontuyau-frontend:dev .

FROM node:24-slim AS build
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
WORKDIR /src/frontend
# pnpm at the version package.json pins ("packageManager"), through corepack.
COPY frontend/package.json frontend/pnpm-lock.yaml ./
RUN corepack enable && pnpm install --frozen-lockfile
COPY frontend/ ./
RUN pnpm run build

# A .NET image, with nothing of .NET in it: Microsoft's "runtime-deps" is Ubuntu 24.04 "chiseled" with only what a
# program needs to run (glibc, libstdc++, OpenSSL, certificates, time zones), no shell, no package manager, non-root
# (app, 1654). Made as the base .NET apps run on, it's also all Node needs; it's the backend's base too, and Microsoft
# rebuilds it monthly (Canonical publishes no general-purpose chiseled image). Node alone is copied in from the
# official image (its own OpenSSL inside), without npm. Only the built files and the server besides: no node_modules
# (the server has no dependency), no pnpm, no sources.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
ARG VERSION=dev
ARG REVISION=unknown
LABEL org.opencontainers.image.title="TonTuyau front" \
      org.opencontainers.image.description="TonTuyau's web app and its server (a self-hosted, YouTube-like video library)" \
      org.opencontainers.image.source="https://github.com/TonTuyau/TonTuyau" \
      org.opencontainers.image.licenses="GPL-3.0-or-later" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}"
ENV NODE_ENV=production \
    PORT=8080 \
    TONTUYAU_BACKEND_URL=http://tontuyau-backend:8080
WORKDIR /app
COPY --from=build /src/frontend/dist ./dist
COPY frontend/server/server.ts frontend/server/previews.ts frontend/server/main.ts ./server/
COPY --from=build /usr/local/bin/node /usr/local/bin/node
EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/node"]
CMD ["server/main.ts"]
