FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY backend/DeepWiki.Api/DeepWiki.Api.csproj backend/DeepWiki.Api/
RUN dotnet restore backend/DeepWiki.Api/DeepWiki.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/DeepWiki.Api/DeepWiki.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:8001
ENV DEEPWIKI_DATA_DIR=/data

EXPOSE 8001

CMD ["dotnet", "DeepWiki.Api.dll"]
