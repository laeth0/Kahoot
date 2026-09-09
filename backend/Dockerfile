FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY *.sln* ./
COPY src/Kahoot.Domain/Kahoot.Domain.csproj src/Kahoot.Domain/
COPY src/Kahoot.Application/Kahoot.Application.csproj src/Kahoot.Application/
COPY src/Kahoot.Infrastructure/Kahoot.Infrastructure.csproj src/Kahoot.Infrastructure/
COPY src/Kahoot.Api/Kahoot.Api.csproj src/Kahoot.Api/

RUN dotnet restore

COPY src/ src/
RUN dotnet publish src/Kahoot.Api/Kahoot.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Kahoot.Api.dll"]
