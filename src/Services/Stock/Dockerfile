FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Services/Stock/Stock.csproj", "src/Services/Stock/"]
COPY ["src/Contracts/TradingSystem.Contracts.csproj", "src/Contracts/"]
RUN dotnet restore "src/Services/Stock/Stock.csproj"
COPY . .
WORKDIR "/src/src/Services/Stock"
RUN dotnet build "./Stock.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Stock.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Stock.dll"]
