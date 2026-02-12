# syntax=docker/dockerfile:1

# Backend Development Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS base
WORKDIR /src

ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true
ENV DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=true

# Copy solution and project files for restore
COPY ["Mizan.sln", "./"]
COPY ["Mizan.Api/Mizan.Api.csproj", "Mizan.Api/"]
COPY ["Mizan.Application/Mizan.Application.csproj", "Mizan.Application/"]
COPY ["Mizan.Domain/Mizan.Domain.csproj", "Mizan.Domain/"]
COPY ["Mizan.Infrastructure/Mizan.Infrastructure.csproj", "Mizan.Infrastructure/"]

# Restore with BuildKit cache
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "Mizan.Api/Mizan.Api.csproj"

# Copy source code
COPY . .

EXPOSE 8080

CMD ["dotnet", "run", "--project", "Mizan.Api/Mizan.Api.csproj", "--urls", "http://0.0.0.0:8080"]
