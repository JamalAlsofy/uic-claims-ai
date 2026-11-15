FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/core/*.csproj src/core/
COPY src/infrastructure/*.csproj src/infrastructure/
COPY src/api/*.csproj src/api/
RUN dotnet restore src/api/Uic.Claims.Ai.Api.csproj
COPY src/ src/
RUN dotnet publish src/api/Uic.Claims.Ai.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Uic.Claims.Ai.Api.dll"]
