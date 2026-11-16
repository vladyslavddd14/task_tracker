# Build context: ./backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, so the layer is cached until a project file changes.
COPY src/TaskTracker.Domain/TaskTracker.Domain.csproj src/TaskTracker.Domain/
COPY src/TaskTracker.Application/TaskTracker.Application.csproj src/TaskTracker.Application/
COPY src/TaskTracker.Infrastructure/TaskTracker.Infrastructure.csproj src/TaskTracker.Infrastructure/
COPY src/TaskTracker.Api/TaskTracker.Api.csproj src/TaskTracker.Api/
RUN dotnet restore src/TaskTracker.Api/TaskTracker.Api.csproj

COPY src/ src/
RUN dotnet publish src/TaskTracker.Api/TaskTracker.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The image provides a non-root user; APP_UID is its id.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskTracker.Api.dll"]
