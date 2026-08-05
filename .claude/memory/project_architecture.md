---
name: ASPS Backend Architecture
description: Core project structure, ports, processes, and tech stack for the ASPS system
type: project
---

## Project Structure
- **ASPSBackend** (port 5555 CQRS, port 50001 alerts, port 50002 notifications) - Business process
- **WebApi** (port 5001) - Presentation/admin process, communicates with ASPSBackend via CQRS Gateway on port 5556
- **Business** - Core domain logic, messaging, views, services
- **Common** - Entities, enums, models, interfaces
- **Interface** - Repository interfaces
- Python PC client at `C:\Jobs\ASPS\apps\desktop\win\src`

## Tech Stack
- .NET 8, MySQL via Pomelo EF Core 7.x
- NetMQ 4.0.1.13 for ZMQ communication
- Newtonsoft.Json with TypeNameHandling.Auto for polymorphic serialization
- ASView is the in-memory cache singleton for users, devices, alerts
