# Technology Stack

**Analysis Date:** 2026-02-13

## Languages

**Primary:**
- C# 12 (.NET 8.0) - Backend services (ASPSBackend14_J/, WebApi/)
- Python 3.9+ - Desktop client, URL analyzer, Python clients
- JavaScript (ES6 Modules) - Chrome extension

**Secondary:**
- SQL - Database schemas and queries
- HTML/CSS - Chrome extension UI and WebApi Razor Pages

## Runtime

**Environment:**
- .NET 8.0 Runtime (net8.0 target framework)
- Python 3.9+ (basic-url-analyzer requires >=3.9)
- Node.js not required (Chrome extension uses vanilla JS modules)
- Chrome Browser Runtime (Manifest V3)

**Package Managers:**
- NuGet - .NET dependencies (managed via .csproj files)
- pip - Python dependencies (requirements.txt, pyproject.toml)
- No npm/yarn - Chrome extension has no build step

## Frameworks

**Core (.NET Backend):**
- ASP.NET Core 8.0 - Web API framework (`c:/Users/pc/Desktop/asps/ASPSBackend14_J/WebApi/WebApi.csproj`)
- Entity Framework Core 7.0.20 - ORM (`c:/Users/pc/Desktop/asps/ASPSBackend14_J/Business/Business.csproj`)
- Pomelo.EntityFrameworkCore.MySql 7.0.0 - MySQL provider for EF Core
- Microsoft.Extensions.Hosting 8.0.0 - Background service hosting
- Swashbuckle.AspNetCore 6.5.0 - Swagger/OpenAPI documentation

**Testing:**
- pytest 8.0+ - Python testing framework (`c:/Users/pc/Desktop/asps/basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/requirements.txt`)

**Build/Dev:**
- MSBuild - .NET compilation (via dotnet CLI)
- PyInstaller 6.0.0+ - Python to EXE packaging (`c:/Users/pc/Desktop/asps/apps/desktop/win/requirements.txt`)

**Python Frameworks:**
- FastAPI 0.109.0+ - REST API server for URL analyzer (`c:/Users/pc/Desktop/asps/basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/requirements.txt`)
- Uvicorn 0.27.0+ - ASGI server for FastAPI
- Pydantic 2.5.0+ - Data validation
- Tkinter/CustomTkinter 5.2.2+ - Desktop app GUI (`c:/Users/pc/Desktop/asps/apps/desktop/win/requirements.txt`)

## Key Dependencies

**Critical (Backend):**
- NetMQ 4.0.1.13 - ZeroMQ messaging for distributed CQRS architecture (`c:/Users/pc/Desktop/asps/ASPSBackend14_J/Business/Business.csproj`, `c:/Users/pc/Desktop/asps/ASPSBackend14_J/WebApi/WebApi.csproj`)
- Newtonsoft.Json 13.0.3 - JSON serialization (all .NET projects)
- Microsoft.AspNetCore.SignalR - Real-time WebSocket communication (WebApi)
- MySqlConnector 2.2.5 - MySQL database driver (via Pomelo.EF)
- NetTopologySuite 2.6.0 - Geospatial data support (`c:/Users/pc/Desktop/asps/ASPSBackend14_J/Business/Business.csproj`)

**Critical (Desktop Client):**
- pyzmq 25.1.0+ - ZeroMQ client for backend communication (`c:/Users/pc/Desktop/asps/apps/desktop/win/requirements.txt`)
- websockets 12.0+ - WebSocket server for extension communication
- requests 2.31.0+ - HTTP client for Google OAuth
- python-dotenv 1.0.1+ - Environment variable management
- pystray 0.19.5+ - System tray integration
- psutil 5.9.0+ - Process monitoring for remote access detection

**Critical (URL Analyzer):**
- playwright 1.40.0+ - Browser automation for web scraping (`c:/Users/pc/Desktop/asps/basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/requirements.txt`)
- beautifulsoup4 4.12.2+ - HTML parsing
- scikit-learn 1.3.2+ - Machine learning classifier
- python-whois 0.8.0+ - WHOIS domain lookup
- validators 0.22.0+ - URL validation

**Infrastructure:**
- Microsoft.Extensions.Logging.Console 8.0.0 - Console logging
- Microsoft.Extensions.Configuration.Json 8.0.0 - JSON configuration
- geoip2 4.8.0+ - IP geolocation (desktop app)
- Pillow 10.0.0+ - Image processing (desktop app)

**Optional:**
- ollama 0.4.0+ - LLM integration for AI explanations (URL analyzer)
- duckduckgo_search 4.0.0+ - Domain reputation checking

## Configuration

**Environment:**
- ASPSBackend: `c:/Users/pc/Desktop/asps/ASPSBackend14_J/ASPSBackend/appsettings.json` - Database connection, NetMQ ports, security keys
- WebApi: `c:/Users/pc/Desktop/asps/ASPSBackend14_J/WebApi/appsettings.json` - HTTP URLs, CQRS endpoint
- Desktop App: `c:/Users/pc/Desktop/asps/apps/desktop/win/.env` - Google OAuth credentials, debug mode
- Desktop App: `c:/Users/pc/Desktop/asps/apps/desktop/win/src/config.py` - Backend endpoints, CURVE keys, port configuration
- URL Analyzer: Environment variables for API keys (optional)

**Build:**
- `*.csproj` files - NuGet package references, target framework
- `c:/Users/pc/Desktop/asps/basic-url-analyzer/basic-url-analyzer/basic-url-analyzer/pyproject.toml` - Python project metadata
- Chrome extension has no build configuration (loads directly)

## Platform Requirements

**Development:**
- Windows 10/11 (primary development platform based on paths)
- .NET 8 SDK
- Python 3.9+
- MySQL 8.0+ server
- Visual Studio 2022 or VS Code with C# extensions
- Chrome browser for extension development

**Production:**
- Server: Linux/Windows server with .NET 8 runtime, MySQL 8.0+
- Desktop Client: Windows 10/11 (can be packaged as standalone .exe via PyInstaller)
- Chrome Extension: Any platform running Chrome/Chromium

**Database:**
- MySQL 8.0+ - Primary data store (ASPSBackend2DB)
- Connection string requires: server, port (3306), database name, user, password, SslMode, AllowPublicKeyRetrieval

**Messaging Infrastructure:**
- NetMQ/ZeroMQ sockets for inter-process communication
- Ports: 5555 (Business endpoint), 5556 (CQRS), 50001 (RealTimeListener), 50002 (NotificationPublisher)
- WebSocket ports for extension: 8080, 8181, 8282, 8383, 8484 (auto-discovery)

---

*Stack analysis: 2026-02-13*
