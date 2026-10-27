FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/BookingService/BookingService.csproj", "src/BookingService/"]
RUN dotnet restore "src/BookingService/BookingService.csproj"

COPY src/BookingService/ src/BookingService/
WORKDIR "/src/src/BookingService"
RUN dotnet publish "BookingService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5003
ENV ASPNETCORE_URLS=http://+:5003
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BookingService.dll"]
