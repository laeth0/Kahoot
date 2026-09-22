FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Kahoot.Api/Kahoot.Api.csproj", "src/Kahoot.Api/"]
COPY ["src/Kahoot.Application/Kahoot.Application.csproj", "src/Kahoot.Application/"]
COPY ["src/Kahoot.Domain/Kahoot.Domain.csproj", "src/Kahoot.Domain/"]
COPY ["src/Kahoot.Infrastructure/Kahoot.Infrastructure.csproj", "src/Kahoot.Infrastructure/"]
RUN dotnet restore "src/Kahoot.Api/Kahoot.Api.csproj"
COPY . .
WORKDIR "/src/src/Kahoot.Api"
RUN dotnet build "Kahoot.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Kahoot.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kahoot.Api.dll"]