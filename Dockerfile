ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0
FROM ${SDK_IMAGE} AS build
WORKDIR /workspace
COPY . .
RUN dotnet restore Backlog.sln --locked-mode --source /workspace/.offline/nuget
RUN dotnet build Backlog.sln -c Release --no-restore
RUN dotnet publish src/Backlog.Api -c Release --no-build --no-restore -o /app

FROM build AS tests
ENTRYPOINT ["dotnet", "test", "Backlog.sln", "-c", "Release", "--no-build", "--no-restore", "--logger", "trx", "--results-directory", "/results"]

FROM ${RUNTIME_IMAGE} AS api
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backlog.Api.dll"]
