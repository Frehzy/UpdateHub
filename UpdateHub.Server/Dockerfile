# Сборка выполняется на машине с интернетом, готовый образ переносится
# в закрытый контур командами 'make save' и 'make load'.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY UpdateHub.Server.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl нужен только для HEALTHCHECK: в базовом образе aspnet его нет,
# и без установки проверка состояния всегда завершалась бы ошибкой.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Каталоги создаются здесь, но при запуске поверх них монтируются том и папка
# Windows, поэтому фактические права приходят с хоста. Контейнер запускается
# от uid 1000 — см. параметр --user в Makefile.
RUN mkdir -p /app/files /app/data && chown -R 1000:1000 /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Опрос каталога и обход раздачи начинаются сразу после старта, поэтому
# первой проверке даётся запас времени.
HEALTHCHECK --interval=30s --timeout=10s --start-period=90s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

EXPOSE 8080

ENTRYPOINT ["dotnet", "UpdateHub.Server.dll"]
