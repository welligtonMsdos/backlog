ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4
FROM ${SDK_IMAGE} AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
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
