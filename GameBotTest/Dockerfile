FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5141
EXPOSE 5140

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["GameBotTest/GameBotTest.csproj", "GameBotTest/"]
COPY ["packages/", "packages/"]

# локальные пакеты
RUN dotnet restore "GameBotTest/GameBotTest.csproj" --source "packages/" --source "https://api.nuget.org/v3/index.json"

COPY . .

WORKDIR "/src/GameBotTest"
RUN dotnet build "GameBotTest.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GameBotTest.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GameBotTest.dll"]
