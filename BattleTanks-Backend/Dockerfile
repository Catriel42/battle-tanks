# ============================================================
# Stage 1: Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BattleTanks-Backend.csproj", "./"]
RUN dotnet restore "BattleTanks-Backend.csproj"

COPY . .

RUN dotnet publish "BattleTanks-Backend.csproj" \
    -c Release \
    -o /app/publish

# ============================================================
# Stage 2: Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copia solo los binarios publicados desde la etapa de build
COPY --from=build /app/publish .

EXPOSE 5000

ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "BattleTanks-Backend.dll"]
