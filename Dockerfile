# syntax=docker/dockerfile:1.7
#
# Build from the repository root:
#   docker build -t orders \
#     --secret id=telerik_key,env=TELERIK_NUGET_KEY \
#     --secret id=telerik_license,env=TELERIK_LICENSE .
#
# The Telerik feed key and licence are build secrets, so they never end up in a layer.

# build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer is cached until the project file changes.
COPY NuGet.config ./
COPY TelerikDemo2/TelerikDemo2.csproj TelerikDemo2/
# The credentials go into a user-level NuGet config; the source itself is already declared in NuGet.config.
RUN --mount=type=secret,id=telerik_key,required=true \
    set -eu; \
    mkdir -p /root/.nuget/NuGet; \
    echo '<configuration />' > /root/.nuget/NuGet/NuGet.Config; \
    dotnet nuget add source https://nuget.telerik.com/v3/index.json \
        --name TelerikOnlineFeed --username api-key --password "$(cat /run/secrets/telerik_key)" \
        --store-password-in-clear-text --configfile /root/.nuget/NuGet/NuGet.Config; \
    dotnet restore TelerikDemo2/TelerikDemo2.csproj; \
    rm -f /root/.nuget/NuGet/NuGet.Config

COPY TelerikDemo2/ TelerikDemo2/
RUN --mount=type=secret,id=telerik_license \
    set -eu; \
    if [ -s /run/secrets/telerik_license ]; then export TELERIK_LICENSE="$(cat /run/secrets/telerik_license)"; fi; \
    dotnet publish TelerikDemo2/TelerikDemo2.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Uploads and data-protection keys; mount volumes here to keep them.
RUN mkdir -p /app/App_Data/uploads /keys && chown -R $APP_UID:$APP_UID /app/App_Data /keys

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    UseHttpsRedirection=false \
    DataProtection__KeysPath=/keys

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TelerikDemo2.dll"]
