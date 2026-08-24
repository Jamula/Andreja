# syntax=docker/dockerfile:1.7
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.301-noble@sha256:ea8bde36c11b6e7eec2656d0e59101d4462f6bd630730f2c8201ed0572b295d5
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.5-noble-chiseled@sha256:1191b4891ae8b1a8184b2de52b2c6332dfb27c30b58d282632044357db63761d

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Andreja.AppHost/Andreja.AppHost.csproj \
    && dotnet publish src/Andreja.AppHost/Andreja.AppHost.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false \
    && mkdir -p /app/state/keys /app/state/attachments

FROM ${DOTNET_RUNTIME_IMAGE} AS final
ARG SOURCE_REVISION=unknown
ARG SOURCE_URL=https://github.com/Jamula/Andreja
LABEL org.opencontainers.image.source="${SOURCE_URL}" \
      org.opencontainers.image.revision="${SOURCE_REVISION}" \
      org.opencontainers.image.licenses="Apache-2.0"
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false \
    DOTNET_EnableDiagnostics=0
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
COPY --from=build --chown=$APP_UID:$APP_UID /app/state /var/lib/andreja
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=6s --start-period=20s --retries=4 \
    CMD ["dotnet", "Andreja.AppHost.dll", "--health-check", "http://127.0.0.1:8080/health/ready"]
ENTRYPOINT ["dotnet", "Andreja.AppHost.dll"]
