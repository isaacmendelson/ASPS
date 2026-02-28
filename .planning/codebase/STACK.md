# Technology Stack

**Analysis Date:** 2026-02-16

## Languages

**Primary:**
- C# 8.0+ - Backend API, business logic, and admin dashboard
- Python 3.x - URL analyzer, desktop app, and system analysis tools

**Secondary:**
- HTML/CSS/JavaScript - Razor Pages for admin dashboard
- SQL - MySQL database scripting and migrations

## Runtime

**Environment:**
- .NET 8.0 - ASPSBackend, WebApi, Business, Common, Interface projects
- Python 3.x - URL analyzers and desktop client (via Docker: python3)

**Package Manager:**
- NuGet - .NET dependencies (csproj manifest files)
- pip - Python dependencies (requirements.txt)
- Lockfile: Not detected for .NET; pip generates pip freeze output as needed

## Frameworks

**Core:**
- ASP.NET Core 8.0 - WebApi (presentation layer with Razor Pages)
- Entity Framework Core 7.0.20 - ORM for database operations
- NetMQ 4.0.1.13 - Distributed messaging (ZeroMQ .NET binding)

**Testing:**
- pytest - Python testing (in requirements.txt)
- xUnit or MSTest - .NET testing (implied but not in shown csproj files)

**Build/Dev:**
- Docker - Multi-stage containerization
  - Stage 1: `mcr.microsoft.com/dotnet/sdk:8.0` for .NET builds
  - Stage 2: `mcr.microsoft.com/dotnet/runtime:8.0` for backend runtime
  - Stage 2: `mcr.microsoft.com/dotnet/aspnet:8.0` for WebApi runtime
- Docker Compose - Orchestration of MySQL, backend, and WebApi services

## Key Dependencies

**Critical:**
- Pomelo.EntityFrameworkCore.MySql 7.0.0 - MySQL database provider for EF Core
- NetMQ 4.0.1.13 - Zero-copy messaging between ASPSBackend and WebApi processes
- Newtonsoft.Json 13.0.3 - JSON serialization across all layers
- NetTopologySuite 2.6.0 - Geospatial data handling (for IP geolocation/alerts)

**Infrastructure:**
- Microsoft.EntityFrameworkCore 7.0.20 - Database abstraction layer
- Microsoft.EntityFrameworkCore.Design 7.0.20 - Migrations tooling
- Microsoft.Extensions.Hosting 8.0.0 - Dependency injection and configuration
- Microsoft.Extensions.Logging - Structured logging
- Microsoft.AspNetCore.SignalR - Real-time WebSocket notifications (WebApi only)

**Web/API:**
- Swashbuckle.AspNetCore 6.5.0 - Swagger/OpenAPI documentation (WebApi)
- Microsoft.AspNetCore.Mvc.NewtonsoftJson 8.0.0 - JSON serialization for MVC

**Python (URL Analyzer):**
- playwright >= 1.40.0 - Headless browser automation for content extraction
- beautifulsoup4 >= 4.12.2 - HTML parsing
- requests >= 2.31.0 - HTTP client for web requests
- python-whois >= 0.8.0 - WHOIS domain lookup
- scikit-learn >= 1.3.2 - Machine learning for phishing detection
- ollama >= 0.4.0 - Local LLM integration (optional, graceful fallback)
- fastapi >= 0.109.0 - REST API server for analyzer
- uvicorn >= 0.27.0 - ASGI server for FastAPI

**Python (Desktop App):**
- pyzmq >= 25.1.0 - ZeroMQ bindings for backend communication
- websockets >= 12.0 - WebSocket for Chrome extension communication
- customtkinter >= 5.2.2 - Modern UI widgets for notifications
- psutil >= 5.9.0 - Process and system monitoring
- geoip2 >= 4.8.0 - IP geolocation (optional, graceful degradation)
- pyinstaller >= 6.0.0 - EXE packaging for Windows

## Configuration

**Environment:**
- `appsettings.json` - Configuration for backend services
  - Database: `ConnectionStrings:DefaultConnection` - MySQL connection string
  - Logging levels: Per-namespace configuration in `Logging:LogLevel`
  - NetMQ ports and endpoints for inter-process messaging
  - Python analyzer path and settings
  - CURVE encryption keys for ZMQ security
  - Token expiration and security settings

- `appsettings.Docker.json` - Docker-specific overrides for containerized deployment

- `.env` file (development only) - Environment variables for desktop app configuration
  - Not committed to git; contains local development settings

**Build:**
- `Dockerfile.backend` - Multi-stage Docker build for ASPSBackend
  - Installs Python 3, Playwright, and browser dependencies
  - Creates Python venv and installs analyzer requirements
  - Exposes: 5555 (NetMQ CQRS), 5556 (CQRS Gateway), 50001 (Alert Listener), 50002 (Publisher)

- `Dockerfile.webapi` - Multi-stage Docker build for WebApi
  - Pure .NET runtime without Python
  - Exposes: 5001 (Admin Dashboard + Swagger + SignalR)

- `docker-compose.yml` - Service orchestration
  - MySQL 8.0 service with volume persistence
  - ASPSBackend service with health checks
  - WebApi service dependent on backend

## Platform Requirements

**Development:**
- .NET 8.0 SDK or higher
- Docker and Docker Compose (for containerized development)
- MySQL 8.0+ server (or use Docker Compose service)
- Python 3.8+ (for analyzer development)
- Visual Studio 2022 or compatible IDE

**Production:**
- Docker runtime
- Docker Compose (for orchestration)
- MySQL 8.0+ database server or compatible
- Linux container host (suggested)

## Additional Notes

- **Version Mismatch:** EntityFrameworkCore 7.0.20 runs on net8.0 - this is supported but consider upgrading EF to 8.x for full parity
- **MySQL Driver:** Pomelo provider uses EF 7.0, not 8.0 - latest is 7.0.0
- **Python Analyzer:** Runs as subprocess called by backend, not as separate service
- **CURVE Encryption:** All NetMQ communication between processes uses ZMQ CURVE for encryption
- **Chrome Extension:** Desktop app communicates with extension via WebSocket on local ports (8080-8484)

---

*Stack analysis: 2026-02-16*
