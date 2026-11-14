# syntax=docker/dockerfile:1
# StemStudio API: ASP.NET Core Minimal API (.NET 10), SQLite + local stem storage under /data.
# Build context: repository root.  docker build -f docker/api.Dockerfile .

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

# ---------------------------------------------------------------- build
FROM ${SDK_IMAGE} AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
WORKDIR /src
# Restore first (cached until a project file or the central package versions change).
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/StemStudio.Domain/StemStudio.Domain.csproj src/StemStudio.Domain/
COPY src/StemStudio.Contracts/StemStudio.Contracts.csproj src/StemStudio.Contracts/
COPY src/StemStudio.Persistence/StemStudio.Persistence.csproj src/StemStudio.Persistence/
COPY src/StemStudio.Api/StemStudio.Api.csproj src/StemStudio.Api/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/StemStudio.Api/StemStudio.Api.csproj
COPY src/StemStudio.Domain/ src/StemStudio.Domain/
COPY src/StemStudio.Contracts/ src/StemStudio.Contracts/
COPY src/StemStudio.Persistence/ src/StemStudio.Persistence/
COPY src/StemStudio.Api/ src/StemStudio.Api/
# No --no-restore: the restore layer can stay cached while the cache mount was pruned (NETSDK1064);
# the implicit restore is a no-op when the packages are present.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/StemStudio.Api/StemStudio.Api.csproj -c Release -o /out -p:UseAppHost=false

# ---------------------------------------------------------------- runtime
FROM ${RUNTIME_IMAGE} AS runtime
# curl only serves the container health check.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /data \
 && chown "$APP_UID":"$APP_UID" /data
ENV ASPNETCORE_HTTP_PORTS=8081 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    StemStudio__DataDir=/data
WORKDIR /app
COPY --from=build /out ./
USER $APP_UID
VOLUME ["/data"]
EXPOSE 8081
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
  CMD ["curl", "--fail", "--silent", "--show-error", "--output", "/dev/null", "http://127.0.0.1:8081/health"]
ENTRYPOINT ["dotnet", "StemStudio.Api.dll"]
