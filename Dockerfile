# syntax=docker/dockerfile:1

# ---- Build: compila e publica somente o projeto web ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Cache NuGet fora de /root para o estágio "migrate" poder rodar sem privilégios
ENV NUGET_PACKAGES=/opt/nuget
# Restore em camada separada para aproveitar cache
COPY Site/HackerToolbox.csproj Site/nuget.config Site/
COPY Migrations.Postgres/HackerToolbox.Migrations.Postgres.csproj Migrations.Postgres/
RUN dotnet restore Migrations.Postgres/HackerToolbox.Migrations.Postgres.csproj \
    && dotnet restore Site/HackerToolbox.csproj

COPY Site/ Site/
COPY Migrations.Postgres/ Migrations.Postgres/
# O publish do Site também compila e copia o assembly de migrations PostgreSQL
RUN dotnet publish Site/HackerToolbox.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Migrations PostgreSQL (ferramenta de uso único; nunca é a imagem final nem roda no startup do site) ----
FROM build AS migrate
RUN dotnet tool install --tool-path /opt/tools dotnet-ef --version 10.0.14
RUN dotnet build Migrations.Postgres/HackerToolbox.Migrations.Postgres.csproj -c Release --no-restore \
    && chmod -R a+rX /opt/nuget /opt/tools \
    && chown -R 1654:1654 /src
# Executa sem privilégios: o código/saída de build em /src são legíveis por todos (root, 755) e o EF só precisa de /tmp
# (tmpfs no Compose). A conexão vem de HBX_PG_* (senha por arquivo/secret), fornecidos em tempo de execução.
USER 1654:1654
ENTRYPOINT ["/opt/tools/dotnet-ef", "database", "update", "--project", "Migrations.Postgres", "--startup-project", "Migrations.Postgres", "--configuration", "Release", "--no-build"]

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# ffmpeg/ffprobe: usados por MetadataScrubber (limpeza de metadados de vídeo/imagem)
# curl: usado apenas pelo HEALTHCHECK
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl \
    && rm -rf /var/lib/apt/lists/*

# /data (SQLite) e /keys (Data Protection) pertencem ao usuário não privilegiado;
# volumes nomeados novos herdam esse dono e as permissões restritas.
RUN mkdir -p /data /keys \
    && chown "$APP_UID:$APP_UID" /data /keys \
    && chmod 700 /data /keys

WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=/data/hackerbox.db" \
    DataProtection__KeysPath=/keys

USER $APP_UID
EXPOSE 8080
VOLUME ["/data", "/keys"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent --show-error "http://127.0.0.1:${ASPNETCORE_HTTP_PORTS}/healthz" || exit 1

ENTRYPOINT ["dotnet", "HackerToolbox.dll"]
