# syntax=docker/dockerfile:1

# ---- build: restore and publish the API together with the Blazor WebAssembly client ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first so that `dotnet restore` is cached until dependencies change.
COPY global.json Directory.Build.props ./
COPY src/KorTrainer.Domain/KorTrainer.Domain.csproj src/KorTrainer.Domain/
COPY src/KorTrainer.Contracts/KorTrainer.Contracts.csproj src/KorTrainer.Contracts/
COPY src/KorTrainer.Application/KorTrainer.Application.csproj src/KorTrainer.Application/
COPY src/KorTrainer.Infrastructure/KorTrainer.Infrastructure.csproj src/KorTrainer.Infrastructure/
COPY src/KorTrainer.UI/KorTrainer.UI.csproj src/KorTrainer.UI/
COPY src/KorTrainer.Web/KorTrainer.Web.csproj src/KorTrainer.Web/
COPY src/KorTrainer.Api/KorTrainer.Api.csproj src/KorTrainer.Api/
RUN dotnet restore src/KorTrainer.Api/KorTrainer.Api.csproj

COPY src/ src/
RUN dotnet publish src/KorTrainer.Api/KorTrainer.Api.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__KorTrainer="Data Source=/data/kortrainer.db" \
    Audio__CacheDirectory=/data/audio

# SQLite lives on a volume owned by the non-root "app" user of the base image.
RUN mkdir -p /data && chown "$APP_UID" /data
VOLUME /data

COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "KorTrainer.Api.dll"]
