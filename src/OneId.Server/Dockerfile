FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/OneId.Server/OneId.Server.csproj", "src/OneId.Server/"]
RUN dotnet restore "src/OneId.Server/OneId.Server.csproj"
COPY . .
WORKDIR "/src/src/OneId.Server"
RUN dotnet build "OneId.Server.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "OneId.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OneId.Server.dll"]
