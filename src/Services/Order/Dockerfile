FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Services/Order/Order.csproj", "src/Services/Order/"]
COPY ["src/Contracts/TradingSystem.Contracts.csproj", "src/Contracts/"]
COPY ["src/Services/Messaging.Abstractions/Messaging.Abstractions.csproj", "src/Services/Messaging.Abstractions/"]
COPY ["src/Services/Messaging/Messaging.csproj", "src/Services/Messaging/"]
COPY ["src/Services/Messaging.EFCore/Messaging.EFCore.csproj", "src/Services/Messaging.EFCore/"]
COPY ["Directory.Packages.props", "./"]
RUN dotnet restore "src/Services/Order/Order.csproj"
COPY . .
WORKDIR "/src/src/Services/Order"
RUN dotnet build "./Order.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Order.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Order.dll"]
