# syntax=docker/dockerfile:1.7
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.404-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled@sha256:7c625e44e5d588b4d7b2a913e94ca4bd5098ce47442da404829e4c7936a764e1

FROM ${DOTNET_SDK_IMAGE} AS build
ARG SOURCE_DATE_EPOCH=0
ENV SOURCE_DATE_EPOCH=${SOURCE_DATE_EPOCH}
WORKDIR /source
COPY . .
RUN --mount=type=bind,from=nuget-cache,target=/nuget-cache,readonly \
    mkdir -p /root/.nuget/packages \
    && cp -a /nuget-cache/. /root/.nuget/packages/ \
    && find . -exec touch --date="@${SOURCE_DATE_EPOCH}" {} + \
    && dotnet restore src/Andreja.AppHost/Andreja.AppHost.csproj \
        --ignore-failed-sources \
        /p:NuGetAudit=false \
        /p:WarningsNotAsErrors=NU1801 \
    && dotnet publish src/Andreja.AppHost/Andreja.AppHost.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false \
        /p:ContinuousIntegrationBuild=true \
        /p:Deterministic=true \
        /p:PathMap=/source=/_/src \
    && fixed_date="$(date --utc --date="@${SOURCE_DATE_EPOCH}" "+%a, %d %b %Y %H:%M:%S GMT")" \
    && sed --in-place --regexp-extended \
        "s/(\"Name\":\"Last-Modified\",\"Value\":)\"[^\"]+\"/\1\"${fixed_date}\"/g" \
        /app/publish/*.staticwebassets.endpoints.json \
    && rm -rf /root/.nuget/packages \
    && mkdir -p /app/state/keys /app/state/attachments

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime-base
ARG SOURCE_REVISION=unknown
ARG SOURCE_URL=https://github.com/Jamula/Andreja
ARG RUNTIME_BASE_NAME=mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled
ARG RUNTIME_BASE_DIGEST=sha256:7c625e44e5d588b4d7b2a913e94ca4bd5098ce47442da404829e4c7936a764e1
LABEL org.opencontainers.image.source="${SOURCE_URL}" \
      org.opencontainers.image.revision="${SOURCE_REVISION}" \
      org.opencontainers.image.base.name="${RUNTIME_BASE_NAME}" \
      org.opencontainers.image.base.digest="${RUNTIME_BASE_DIGEST}" \
      org.opencontainers.image.licenses="Apache-2.0"
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false \
    DOTNET_EnableDiagnostics=0
WORKDIR /app
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=6s --start-period=20s --retries=4 \
    CMD ["dotnet", "Andreja.AppHost.dll", "--health-check", "http://127.0.0.1:8080/health/ready"]
ENTRYPOINT ["dotnet", "Andreja.AppHost.dll"]

FROM runtime-base AS final
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
COPY --from=build --chown=$APP_UID:$APP_UID /app/state /var/lib/andreja
