# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
WORKDIR /src

# Copy csproj files for dependency restoration
COPY ErpBoulder.Api/ErpBoulder.Api.csproj ErpBoulder.Api/
COPY ErpBoulder.Application/ErpBoulder.Application.csproj ErpBoulder.Application/
COPY ErpBoulder.Domain/ErpBoulder.Domain.csproj ErpBoulder.Domain/
COPY ErpBoulder.Infrastructure/ErpBoulder.Infrastructure.csproj ErpBoulder.Infrastructure/
RUN dotnet restore ErpBoulder.Api/ErpBoulder.Api.csproj

# Copy everything else and build
COPY . .
RUN dotnet publish ErpBoulder.Api/ErpBoulder.Api.csproj -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production
WORKDIR /app
COPY --from=build /app/publish .

# Create non-root user and group for running the app (idempotent)
RUN if ! getent group app >/dev/null; then groupadd -r app; fi \
    && if ! id appuser >/dev/null 2>&1; then useradd -r -g app appuser; fi \
    && chown -R appuser:app /app
USER appuser

EXPOSE 5000
ENTRYPOINT ["dotnet", "ErpBoulder.Api.dll"]
