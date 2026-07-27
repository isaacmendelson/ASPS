# -------------------------
# Build stage
# -------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files first for better Docker layer caching
COPY ["WebApi/WebApi.csproj", "WebApi/"]
COPY ["Business/Business.csproj", "Business/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["Interface/Interface.csproj", "Interface/"]

RUN dotnet restore "WebApi/WebApi.csproj"

# Copy the full solution source
COPY . .

WORKDIR "/src/WebApi"

RUN dotnet publish "WebApi.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# -------------------------
# Runtime stage
# -------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Docker

EXPOSE 8080

ENTRYPOINT ["dotnet", "WebApi.dll"]