FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props JobsMarketplace.sln ./
COPY src/Marketplace.Core/Marketplace.Core.csproj src/Marketplace.Core/
COPY src/Marketplace.Persistence/Marketplace.Persistence.csproj src/Marketplace.Persistence/
COPY src/Marketplace.Api/Marketplace.Api.csproj src/Marketplace.Api/
RUN dotnet restore src/Marketplace.Api/Marketplace.Api.csproj
COPY src/ src/
RUN dotnet publish src/Marketplace.Api/Marketplace.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080
ENTRYPOINT ["dotnet", "Marketplace.Api.dll"]
