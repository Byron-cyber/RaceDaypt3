# syntax=docker/dockerfile:1
# ---------- Stage 1: build & publish ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the project file first so the restore layer is cached
COPY RaceDay.Web/RaceDay.Web.csproj RaceDay.Web/
RUN dotnet restore RaceDay.Web/RaceDay.Web.csproj

# Copy the rest of the MVC source and publish
COPY RaceDay.Web/ RaceDay.Web/
RUN dotnet publish RaceDay.Web/RaceDay.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: small runtime image ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The MVC app only talks to the API. Override at run time with -e ApiSettings__BaseUrl=...
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ApiSettings__BaseUrl=http://host.docker.internal:5274

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
