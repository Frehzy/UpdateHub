# Сборка выполняется на машине с интернетом, готовый образ переносится
# в закрытый контур командами 'make save' и 'make load'.
#
# Контекст сборки — корень репозитория: сервер ссылается на общую библиотеку
# контрактов и на панель управления, и оба каталога обязаны попасть внутрь.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только файлы проектов: пока они не менялись, docker берёт слой
# с восстановленными пакетами из кэша, а не тянет их заново.
COPY UpdateHub.BackendServer/UpdateHub.BackendServer.csproj UpdateHub.BackendServer/
COPY UpdateHub.Shared/UpdateHub.Shared.csproj UpdateHub.Shared/
COPY UpdateHub.FrontendServer/UpdateHub.FrontendServer.csproj UpdateHub.FrontendServer/
RUN dotnet restore UpdateHub.BackendServer/UpdateHub.BackendServer.csproj

COPY UpdateHub.BackendServer/ UpdateHub.BackendServer/
COPY UpdateHub.Shared/ UpdateHub.Shared/
COPY UpdateHub.FrontendServer/ UpdateHub.FrontendServer/

# Публикация сервера собирает и панель управления: она подключена ссылкой
# на проект, и её файлы попадают в wwwroot.
RUN dotnet publish UpdateHub.BackendServer/UpdateHub.BackendServer.csproj -c Release -o /app/publish --no-restore

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
RUN mkdir -p /app/files /app/data /app/backup && chown -R 1000:1000 /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Опрос каталога и обход раздачи начинаются сразу после старта, поэтому
# первой проверке даётся запас времени.
HEALTHCHECK --interval=30s --timeout=10s --start-period=90s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

EXPOSE 8080

ENTRYPOINT ["dotnet", "UpdateHub.BackendServer.dll"]
