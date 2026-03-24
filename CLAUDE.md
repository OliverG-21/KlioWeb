# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Klio is a C# (.NET 8.0) image management system built with Blazor Server. It runs on Raspberry Pi (ARM64) and macOS for development.

## Architecture

```
Klio/
├── src/
│   ├── Klio.Core/           # Domain models + interfaces (no dependencies)
│   ├── Klio.Infrastructure/ # EF Core, repositories, services (ImageSharp, MetadataExtractor)
│   ├── Klio.Worker/         # BackgroundService: FileWatcherService + ImageProcessorService
│   └── Klio.Web/            # Blazor Server app (hosts worker background services too)
├── docker-compose.yml            # PostgreSQL for local dev
└── Klio.sln
```

## Commands

```bash
# Build
dotnet build Klio.sln

# Run web app (combined with worker)
dotnet run --project src/Klio.Web

# Run worker standalone
dotnet run --project src/Klio.Worker

# EF Core migrations (from repo root)
dotnet ef migrations add <Name> --project src/Klio.Infrastructure --startup-project src/Klio.Web
dotnet ef database update --project src/Klio.Infrastructure --startup-project src/Klio.Web

# Start local postgres
docker compose up -d
```

## Key Design Decisions

- **Target framework**: net8.0 (SDK available; update to net10.0 when SDK is available)
- **Database**: PostgreSQL via EF Core 8.x + Npgsql 8.x
- **Image processing**: SixLabors.ImageSharp (ARM-compatible, pure .NET)
- **EXIF**: MetadataExtractor library
- **Auth**: Cookie authentication with BCrypt password hashing (no ASP.NET Identity)
- **Queue**: System.Threading.Channels (in-process, no external broker)
- **Combined deployment**: Klio.Web hosts both Blazor Server and the Worker background services

## Default credentials

- Admin password: `admin` (BCrypt hash stored in AppSettings table, seeded on first run)
- Login at: `/admin/login`

## Storage layout (default: /tmp/klio for dev)

```
/tmp/klio/
├── inbox/          # Drop zone / FileSystemWatcher target
├── duplicates/     # Duplicates moved here
├── thumbnails/     # Generated thumbnails (_thumb.jpg suffix)
├── nodate/         # Images without EXIF date
└── YYYY/MM/DD/     # Date-organized images
```

## Image serving

Images and thumbnails are served via Razor Pages (not static files):
- `/images/thumbnail/{id}` - serve thumbnail (falls back to full image)
- `/images/serve/{id}` - serve full image

## Admin pages (require auth cookie)

- `/admin/login` - password form (Razor Page)
- `/admin` - dashboard
- `/admin/topics` - topic CRUD
- `/admin/duplicates` - duplicate log
- `/admin/upload` - manual file upload
- `/admin/settings` - change password, storage paths, thumbnail size

## User pages (no auth)

- `/` or `/gallery` - gallery grid, filterable by topic
- `/map` - Leaflet.js map with GPS image pins
- `/topic/{id}` - images in a specific topic
