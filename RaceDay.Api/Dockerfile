# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY RaceDay.Api/RaceDay.Api.csproj RaceDay.Api/
RUN dotnet restore RaceDay.Api/RaceDay.Api.csproj
COPY RaceDay.Api/ RaceDay.Api/
RUN dotnet publish RaceDay.Api/RaceDay.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
