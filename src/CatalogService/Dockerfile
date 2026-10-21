FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/CatalogService/CatalogService.csproj", "src/CatalogService/"]
RUN dotnet restore "src/CatalogService/CatalogService.csproj"

COPY src/CatalogService/ src/CatalogService/
WORKDIR "/src/src/CatalogService"
RUN dotnet publish "CatalogService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5002
ENV ASPNETCORE_URLS=http://+:5002
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CatalogService.dll"]
