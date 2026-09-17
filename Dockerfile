FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY gh-rotas-api/gh-rotas-api.csproj gh-rotas-api/
RUN dotnet restore gh-rotas-api/gh-rotas-api.csproj
COPY gh-rotas-api/ gh-rotas-api/
RUN dotnet publish gh-rotas-api/gh-rotas-api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "gh-rotas-api.dll"]
