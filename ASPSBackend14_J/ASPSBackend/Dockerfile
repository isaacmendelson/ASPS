# ============================================================
# ASPSBackend Dockerfile — Azure Container Apps (az acr build)
# ============================================================
# NOTE ON BUILD CONTEXT — unlike ASPSBackend14_J/WebApi/Dockerfile
# (context = ASPSBackend14_J/), this Dockerfile needs the repo
# ROOT as build context, because it copies the Python Analyzers/
# directory, which lives at <repo-root>/Analyzers/ — a sibling of
# ASPSBackend14_J/, not a child of it.
#
# Build from the repo root:
#   az acr build --registry <registry> \
#     -t asps-backend:<tag> \
#     -f ASPSBackend14_J/ASPSBackend/Dockerfile .
#
# (local docker equivalent: docker build -f ASPSBackend14_J/ASPSBackend/Dockerfile -t asps-backend:<tag> .)
# ============================================================

# -------------------------
# Build stage
# -------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files first for better Docker layer caching
COPY ["ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj", "ASPSBackend14_J/ASPSBackend/"]
COPY ["ASPSBackend14_J/Business/Business.csproj", "ASPSBackend14_J/Business/"]
COPY ["ASPSBackend14_J/Common/Common.csproj", "ASPSBackend14_J/Common/"]
COPY ["ASPSBackend14_J/Interface/Interface.csproj", "ASPSBackend14_J/Interface/"]

RUN dotnet restore "ASPSBackend14_J/ASPSBackend/ASPSBackend.csproj"

# Copy the full solution source
COPY ASPSBackend14_J/ ASPSBackend14_J/

WORKDIR "/src/ASPSBackend14_J/ASPSBackend"

RUN dotnet publish "ASPSBackend.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# -------------------------
# Runtime stage
# -------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Install Python 3.11 + Playwright so the Backend host can invoke the
# Analyzers/ microservices via subprocess (AD-1 Phase 1 — see
# docs/cloud/AZURE_DEPLOYMENT_GUIDE.md and ADR-003).
RUN apt-get update && apt-get install -y python3 python3-pip python3-venv && \
    python3 -m pip install --break-system-packages playwright scikit-learn requests && \
    python3 -m playwright install chromium && \
    python3 -m playwright install-deps && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

# Python analyzer microservices (invoked via subprocess, not HTTP — Phase 2/ASPS-708 will split this out)
COPY Analyzers/ /app/Analyzers/

ENV ASPNETCORE_ENVIRONMENT=Docker

# NetMQ ports — 5556 CQRS gateway (WebApi <-> Backend), 50001 real-time
# alert listener, 50002 notification publisher. No HTTP port: this is a
# backend host service, not a web app.
EXPOSE 5556 50001 50002

ENTRYPOINT ["dotnet", "ASPSBackend.dll"]
