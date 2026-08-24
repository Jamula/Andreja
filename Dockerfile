# syntax=docker/dockerfile:1.7
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.301-noble@sha256:ea8bde36c11b6e7eec2656d0e59101d4462f6bd630730f2c8201ed0572b295d5
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.11-noble-chiseled@sha256:0839314d08bb65da369135389a5d8291f75ace587fbb0488f469eb92c62eef68

FROM ${DOTNET_SDK_IMAGE} AS build
ARG SOURCE_DATE_EPOCH=0
ENV SOURCE_DATE_EPOCH=${SOURCE_DATE_EPOCH}
WORKDIR /source
COPY . .
RUN find . -exec touch --date="@${SOURCE_DATE_EPOCH}" {} + \
    && dotnet restore src/Andreja.AppHost/Andreja.AppHost.csproj \
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
    && mkdir -p /app/state/keys /app/state/attachments

FROM ${DOTNET_SDK_IMAGE} AS supply-build
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
ARG RUNTIME_BASE_NAME=mcr.microsoft.com/dotnet/aspnet:10.0.11-noble-chiseled
ARG RUNTIME_BASE_DIGEST=sha256:0839314d08bb65da369135389a5d8291f75ace587fbb0488f469eb92c62eef68
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

FROM runtime-base AS supply-final
COPY --from=supply-build --chown=$APP_UID:$APP_UID /app/publish .
COPY --from=supply-build --chown=$APP_UID:$APP_UID /app/state /var/lib/andreja

FROM runtime-base AS final
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
COPY --from=build --chown=$APP_UID:$APP_UID /app/state /var/lib/andreja
