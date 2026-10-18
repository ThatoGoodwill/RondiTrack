# syntax=docker/dockerfile:1

# ---------- build stage: has the SDK, never shipped ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1. Copy ONLY the project files and restore. This layer is cached until a package or reference changes.
COPY Domain/Domain.csproj Domain/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY RondiTrack.Api/RondiTrack.Api.csproj RondiTrack.Api/
RUN dotnet restore RondiTrack.Api/RondiTrack.Api.csproj

# 2. Now copy the source (changes often) and publish. The test project is never copied.
COPY Domain/ Domain/
COPY Infrastructure/ Infrastructure/
COPY RondiTrack.Api/ RondiTrack.Api/
RUN dotnet publish RondiTrack.Api/RondiTrack.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime stage: ONLY the published output ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
# The base image already contains the non-root user "app"; never run as root.
USER $APP_UID
ENTRYPOINT ["dotnet", "RondiTrack.Api.dll"]
