# syntax=docker/dockerfile:1
# StemStudio web: Blazor Web App (.NET 10, Interactive Server) with two TypeScript bundles built
# here (both git-ignored): the audio engine (src/audio-engine -> wwwroot/js/audio-engine.js) and the
# browser glue of feature 004 (src/web-interop -> wwwroot/js/web-interop.js + first-paint.js), and
# the SoundTouch module of feature 012 (LGPL-2.1, ADR-021): scripts/build-soundtouch.sh compiles
# wwwroot/js/soundtouch/soundtouch.wasm from third_party/soundtouch/soundtouch.lock offline and
# writes the distribution folder wwwroot/third-party/soundtouch/ (licence, notice, exact source
# archive, shim, header, lock, build scripts, SHA256SUMS) that every image ships and serves.
# Build context: repository root.  docker build -f docker/web.Dockerfile .

ARG NODE_IMAGE=node:22-slim
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0
# Must equal EMSDK_IMAGE@EMSDK_DIGEST of third_party/soundtouch/soundtouch.lock
# (scripts/build-soundtouch.sh --check-pins, run in CI).
ARG EMSDK_IMAGE=emscripten/emsdk:6.0.13@sha256:fb7864e398acfda863dfcea78b8bc8adad359fa60c6a0bbeb845931de64aa68a

# ---------------------------------------------------------------- SoundTouch module (012)
FROM ${EMSDK_IMAGE} AS soundtouch
WORKDIR /work
COPY third_party/soundtouch/ third_party/soundtouch/
COPY scripts/build-soundtouch.sh scripts/build-soundtouch.ps1 scripts/
# Download + SHA-256 check is the only step with network; the compile runs without it.
RUN bash scripts/build-soundtouch.sh --fetch-only
RUN --network=none bash scripts/build-soundtouch.sh --local --out /out/js/soundtouch/soundtouch.wasm  && bash scripts/build-soundtouch.sh --package /out/third-party/soundtouch

# ---------------------------------------------------------------- engine bundle
FROM ${NODE_IMAGE} AS engine
WORKDIR /src/src/audio-engine
COPY src/audio-engine/package.json src/audio-engine/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm \
    npm ci --no-audit --no-fund
COPY src/audio-engine/ ./
# vite writes to ../StemStudio.Web/wwwroot/js/audio-engine.js (see vite.config.ts)
RUN npm run build

# ---------------------------------------------------------------- web-interop bundles
FROM ${NODE_IMAGE} AS interop
WORKDIR /src/src/web-interop
COPY src/web-interop/package.json src/web-interop/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm \
    npm ci --no-audit --no-fund
COPY src/web-interop/ ./
# Feature 014 (web-interop 1.1.0 section 5.4): catalogue.ts imports the key map catalogue at build time,
# the same JSON file KeyMapDefaults reads in C#.
COPY src/StemStudio.Web/wwwroot/js/keymap-defaults.json ../StemStudio.Web/wwwroot/js/keymap-defaults.json
# vite writes web-interop.js and first-paint.js to ../StemStudio.Web/wwwroot/js/ (see vite.config.ts)
RUN npm run build

# ---------------------------------------------------------------- build
FROM ${SDK_IMAGE} AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/StemStudio.Contracts/StemStudio.Contracts.csproj src/StemStudio.Contracts/
COPY src/StemStudio.Web/StemStudio.Web.csproj src/StemStudio.Web/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/StemStudio.Web/StemStudio.Web.csproj
COPY src/StemStudio.Contracts/ src/StemStudio.Contracts/
COPY src/StemStudio.Web/ src/StemStudio.Web/
COPY --from=engine /src/src/StemStudio.Web/wwwroot/js/ src/StemStudio.Web/wwwroot/js/
COPY --from=interop /src/src/StemStudio.Web/wwwroot/js/ src/StemStudio.Web/wwwroot/js/
# Before publish, so MapStaticAssets knows the files (.wasm is served as application/wasm).
COPY --from=soundtouch /out/ src/StemStudio.Web/wwwroot/
# No --no-restore: the restore layer can stay cached while the cache mount was pruned (NETSDK1064);
# the implicit restore is a no-op when the packages are present.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/StemStudio.Web/StemStudio.Web.csproj -c Release -o /out -p:UseAppHost=false

# ---------------------------------------------------------------- runtime
FROM ${RUNTIME_IMAGE} AS runtime
# curl only serves the container health check. The data-protection key ring (antiforgery,
# Blazor circuits) lives in a volume so a container restart does not invalidate open pages.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /home/app/.aspnet/DataProtection-Keys \
 && chown -R "$APP_UID":"$APP_UID" /home/app/.aspnet
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /app
COPY --from=build /out ./
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
  CMD ["curl", "--fail", "--silent", "--show-error", "--output", "/dev/null", "http://127.0.0.1:8080/health"]
ENTRYPOINT ["dotnet", "StemStudio.Web.dll"]
