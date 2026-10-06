# syntax=docker/dockerfile:1
# Fatoura API — build: docker build -t fatoura-api backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Fatoura.Domain/Fatoura.Domain.csproj src/Fatoura.Domain/
COPY src/Fatoura.Api/Fatoura.Api.csproj src/Fatoura.Api/
RUN dotnet restore src/Fatoura.Api/Fatoura.Api.csproj
COPY src/ src/
RUN dotnet publish src/Fatoura.Api/Fatoura.Api.csproj -c Release -o /app --no-restore

# Debian-based runtime (not Alpine): QuestPDF's native Skia library needs glibc.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Fatoura.Api.dll"]
