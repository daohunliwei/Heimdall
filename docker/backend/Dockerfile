FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json NuGet.config ./
COPY backend/Heimdall.Api/Heimdall.Api.csproj backend/Heimdall.Api/
COPY backend/Heimdall.Core/Heimdall.Core.csproj backend/Heimdall.Core/
COPY backend/Heimdall.Infrastructure/Heimdall.Infrastructure.csproj backend/Heimdall.Infrastructure/
COPY backend/Heimdall.Repository/Heimdall.Repository.csproj backend/Heimdall.Repository/
RUN dotnet restore backend/Heimdall.Api/Heimdall.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/Heimdall.Api/Heimdall.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:8001
ENV HEIMDALL_DATA_DIR=/data

EXPOSE 8001

CMD ["dotnet", "Heimdall.Api.dll"]
