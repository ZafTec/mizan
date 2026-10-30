# Prices worker. Build context is the repository root: the image needs
# backend/Mizan.Contracts as well as prices/, and prices/Dockerfile.dockerignore
# keeps everything else out of the context.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["backend/Mizan.Contracts/Mizan.Contracts.csproj", "backend/Mizan.Contracts/"]
COPY ["prices/Mizan.Prices/Mizan.Prices.csproj", "prices/Mizan.Prices/"]
RUN dotnet restore "prices/Mizan.Prices/Mizan.Prices.csproj"

COPY backend/Mizan.Contracts/ backend/Mizan.Contracts/
COPY prices/Mizan.Prices/ prices/Mizan.Prices/
RUN dotnet publish "prices/Mizan.Prices/Mizan.Prices.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

EXPOSE 5003
ENV ASPNETCORE_URLS=http://+:5003
ENTRYPOINT ["dotnet", "Mizan.Prices.dll"]
