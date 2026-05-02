FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/MatchMaking.Contracts/MatchMaking.Contracts.csproj src/MatchMaking.Contracts/
COPY src/MatchMaking.Worker/MatchMaking.Worker.csproj src/MatchMaking.Worker/
RUN dotnet restore src/MatchMaking.Worker/MatchMaking.Worker.csproj

COPY src/ src/
RUN dotnet publish src/MatchMaking.Worker/MatchMaking.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MatchMaking.Worker.dll"]
