# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada separada (cache)
COPY EasyFrete.Api/EasyFrete.Api.csproj EasyFrete.Api/
RUN dotnet restore EasyFrete.Api/EasyFrete.Api.csproj

COPY . .
RUN dotnet publish EasyFrete.Api/EasyFrete.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
# ICU/tzdata para cultura pt-BR e fuso America/Sao_Paulo
RUN apk add --no-cache icu-libs tzdata
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0

WORKDIR /app
COPY --from=build /app/publish .

# /app/data com dono app (UID 1654)
RUN mkdir -p /app/data && chown -R 1654:1654 /app/data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "EasyFrete.Api.dll"]