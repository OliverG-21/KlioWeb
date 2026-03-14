# Projekt42 — Self-Hosted Photo Gallery

Selbst gehostete Foto-Galerie-Applikation für den Raspberry Pi. Fotos werden automatisch nach EXIF-Datum einsortiert, mit Thumbnails versehen und in einer PostgreSQL-Datenbank katalogisiert. Die Weboberfläche bietet eine öffentliche Galerie sowie einen passwortgeschützten Admin-Bereich.

---

## Inhaltsverzeichnis

- [Architektur](#architektur)
- [Projektstruktur](#projektstruktur)
- [Datenmodell](#datenmodell)
- [Verarbeitungs-Pipeline](#verarbeitungs-pipeline)
- [Web-Routen & Seiten](#web-routen--seiten)
- [Admin-Funktionen](#admin-funktionen)
- [Technologie-Stack](#technologie-stack)
- [Lokale Entwicklung](#lokale-entwicklung)
- [Konfiguration](#konfiguration)
- [Bekannte Besonderheiten & Hinweise](#bekannte-besonderheiten--hinweise)

---

## Architektur

```
┌─────────────────────────────────────────────────────┐
│                   Projekt42.Web                      │
│  ASP.NET Core — Razor Pages + Alpine.js             │
│                                                     │
│  Öffentlich: Gallery, TopicView, Map                │
│  Admin:      Upload, Alben, Fotos, Einstellungen    │
│                                                     │
│  ┌──────────────────────────────────────────────┐   │
│  │            Background Workers                │   │
│  │  FileWatcherService  │  ImageProcessorService│   │
│  └──────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────┘
                         │ DI / Scoped Services
          ┌──────────────┴──────────────┐
          │                             │
┌─────────▼──────────┐      ┌───────────▼──────────┐
│  Projekt42.Core    │      │ Projekt42.Infra       │
│  Models            │      │ EF Core Repositories  │
│  Interfaces        │      │ FileOrganizerService  │
└────────────────────┘      │ ExifService           │
                            │ ThumbnailService      │
                            │ HashService           │
                            │ ProcessingChannel     │
                            └───────────┬───────────┘
                                        │
                            ┌───────────▼───────────┐
                            │      PostgreSQL        │
                            └───────────────────────┘
```

Die Applikation ist ein einzelner ASP.NET Core Host, der gleichzeitig den Webserver und die Background-Worker betreibt. Ein separates `Projekt42.Worker`-Projekt enthält die Worker-Klassen, wird aber als Abhängigkeit in `Projekt42.Web` eingebunden.

---

## Projektstruktur

```
Projekt42/
├── docker-compose.yml          # PostgreSQL für lokale Entwicklung
├── global.json                 # .NET SDK Version (10.0.201)
├── Projekt42.sln
└── src/
    ├── Projekt42.Core/         # Domain — keine externen Abhängigkeiten
    │   ├── Models/             # Image, Topic, AlbumFolder, AppSettings, DuplicateLog
    │   └── Interfaces/         # IImageRepository, ITopicRepository, IFileOrganizerService, ...
    │
    ├── Projekt42.Infrastructure/  # Datenzugriff & Services
    │   ├── Data/               # AppDbContext, Migrations
    │   ├── Repositories/       # EF Core Implementierungen der Interfaces
    │   └── Services/           # FileOrganizerService, ExifService, ThumbnailService, ...
    │
    ├── Projekt42.Web/          # ASP.NET Core Host
    │   ├── Pages/              # Razor Pages (Gallery + Admin)
    │   │   ├── Admin/          # Passwortgeschützter Bereich
    │   │   ├── Images/         # Thumbnail- und Vollbild-Endpoints
    │   │   └── Shared/         # _Layout.cshtml, _AdminLayout.cshtml
    │   ├── wwwroot/            # Bootstrap, Alpine.js, Leaflet, CSS
    │   └── Program.cs          # DI-Registrierung, Middleware, DB-Migration
    │
    └── Projekt42.Worker/       # BackgroundService-Klassen
        ├── FileWatcherService.cs
        └── ImageProcessorService.cs
```

---

## Datenmodell

```
AlbumFolder (self-referencing)
├── Id, Name
├── ParentFolderId → AlbumFolder (nullable, Restrict on delete)
└── SubFolders, Albums (Navigation)

Topic (= Album)
├── Id, Name, Description
├── FolderId → AlbumFolder (nullable, SetNull on delete)
└── Images (Navigation)

Image
├── Id
├── OriginalFilename, StoredFilename, FilePath, ThumbnailPath
├── FileSizeBytes, Hash (SHA-256, unique index)
├── MimeType
├── DateTaken (DateTime?, UTC, aus EXIF)
├── GpsLatitude, GpsLongitude (double?, aus EXIF)
├── TopicId → Topic (nullable, SetNull on delete)
└── ProcessedAt, CreatedAt

DuplicateLog
├── IncomingFilename, DuplicatePath
├── ExistingImageId, ExistingImagePath
└── DetectedAt

AppSettings (singleton)
├── AdminPasswordHash (BCrypt)
├── StorageBasePath, InboxPath
└── ThumbnailWidth, ThumbnailHeight
```

**Ordner-Hierarchie:** `AlbumFolder` ist selbst-referenzierend und unterstützt beliebige Tiefe. Topics (Alben) liegen immer in einem Leaf-Folder oder ohne Folder-Zuordnung.

---

## Verarbeitungs-Pipeline

```
Upload (Web)  ──┐
                ├──► IProcessingQueue  ◄──  FileWatcherService (Inbox-Ordner)
                │    (Channel<(path, topicId?)>)
                │
                └──► ImageProcessorService (BackgroundService)
                          │
                          ▼
                    FileOrganizerService.OrganizeAsync(path, topicId?)
                          │
                          ├─ HashService.ComputeAsync()        → SHA-256
                          ├─ IImageRepository.GetByHashAsync() → Duplikat?
                          │    └─ ja: Datei → duplicates/, DuplicateLog anlegen
                          │
                          ├─ ExifService.ExtractAsync()        → DateTaken, GPS
                          │
                          ├─ Dateipfad berechnen:
                          │    DateTaken vorhanden → storage/YYYY/MM/DD/
                          │    sonst              → storage/nodate/YYYY/
                          │
                          ├─ File.Move() inbox → storage
                          │    (falls Zieldatei schon existiert: inbox löschen)
                          │
                          ├─ ThumbnailService.GenerateAsync()  → JPEG, max 300×300
                          │
                          └─ IImageRepository.AddAsync()       → DB-Eintrag
```

**Wichtig:** Die `topicId` wird von der Queue bis zum DB-Eintrag durchgereicht. Fotos, die über den Inbox-Ordner (FileWatcher) eintreffen, erhalten `topicId = null`.

---

## Web-Routen & Seiten

### Öffentlich

| Route | Seite | Beschreibung |
|-------|-------|--------------|
| `/` | Index | Alle Fotos als Grid, Alpine.js Lightbox |
| `/topic/{topicId:int}` | TopicView | Fotos eines Albums, Lightbox |
| `/map` | Map | Leaflet-Karte mit GPS-Markern |
| `/images/thumbnail/{id}` | Thumbnail | Thumbnail ausliefern |
| `/images/serve/{id}` | Serve | Vollbild ausliefern |

### Admin (Cookie-Auth erforderlich)

| Route | Seite | Beschreibung |
|-------|-------|--------------|
| `/admin` | Dashboard | Übersicht |
| `/admin/login` | Login | Authentifizierung |
| `/admin/alben` | Alben | Ordner- und Albumverwaltung (CRUD) |
| `/admin/album/{topicId:int}` | AlbumPhotos | Fotos im Album, verschieben |
| `/admin/unassigned` | UnassignedPhotos | Nicht zugeordnete Fotos einem Album zuweisen |
| `/admin/upload` | Upload | Fotos hochladen, direkt Album zuordnen |
| `/admin/duplicates` | Duplicates | Duplikat-Log |
| `/admin/settings` | Settings | Passwort, Pfade, Thumbnail-Größe |

---

## Admin-Funktionen

### Alben-Verwaltung (`/admin/alben`)
- Hierarchische Ordnerstruktur (unbegrenzte Tiefe) anlegen, umbenennen, verschieben, löschen
- Alben (Topics) innerhalb von Leaf-Foldern erstellen, bearbeiten, löschen
- Alben zwischen Ordnern verschieben

### Upload (`/admin/upload`)
- Mehrere Fotos gleichzeitig (Limit: 1 GB pro Request)
- Albumzuordnung beim Upload: kein Album / vorhandenes Album / neues Album anlegen
- Unterstützte Formate: JPEG, PNG, HEIC

### Foto-Verwaltung
- **Album-Fotos** (`/admin/album/{id}`): Fotos einzeln oder als Bulk in ein anderes Album verschieben
- **Nicht zugeordnet** (`/admin/unassigned`): Alle Fotos ohne Album-Zuordnung, einzeln oder bulk zuweisen

### Duplikat-Erkennung
Automatisch bei jeder Verarbeitung via SHA-256-Hash. Duplikate werden in `{storage}/duplicates/` verschoben und geloggt. Kein Fehler, kein Datenverlust.

---

## Technologie-Stack

| Bereich | Technologie |
|---------|-------------|
| Framework | .NET 10, ASP.NET Core Razor Pages |
| Frontend | Alpine.js v3 (Reaktivität, Lightbox), Bootstrap 5, Bootstrap Icons |
| Karten | Leaflet.js (GPS-Ansicht) |
| Datenbank | PostgreSQL 16 via Entity Framework Core + Npgsql |
| Bildverarbeitung | SixLabors.ImageSharp (ARM-kompatibel) |
| EXIF | MetadataExtractor |
| Passwörter | BCrypt.Net |
| Queueing | System.Threading.Channels (in-process, kein externer Broker) |
| Auth | Cookie-Authentifizierung (8h sliding expiration) |

---

## Lokale Entwicklung

### Voraussetzungen
- .NET 10 SDK (`global.json` pinnt auf `10.0.201`)
- Docker (für PostgreSQL)

### Setup

```bash
# 1. PostgreSQL starten
docker-compose up -d

# 2. Abhängigkeiten wiederherstellen
dotnet restore

# 3. Datenbank migrieren + App starten
cd src/Projekt42.Web
dotnet run
```

Die App startet auf `http://localhost:5005`. Die Datenbank wird beim ersten Start automatisch migriert und mit Standardwerten befüllt (Admin-Passwort: `admin`).

### Migrations

```bash
# Neue Migration anlegen
dotnet ef migrations add <Name> --project src/Projekt42.Infrastructure --startup-project src/Projekt42.Web

# Datenbank aktualisieren
dotnet ef database update --project src/Projekt42.Infrastructure --startup-project src/Projekt42.Web
```

### Standard-Konfiguration (AppSettings)

| Einstellung | Standardwert |
|-------------|-------------|
| `StorageBasePath` | `/tmp/projekt42` |
| `InboxPath` | `/tmp/projekt42/inbox` |
| `ThumbnailWidth` | 300 px |
| `ThumbnailHeight` | 300 px |
| Admin-Passwort | `admin` |

---

## Konfiguration

### Connection String

In `appsettings.json` oder als Umgebungsvariable:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=projekt42;Username=projekt42;Password=projekt42dev"
  }
}
```

### Speicherpfade

Werden in der Datenbank (`AppSettings`) gespeichert und über `/admin/settings` geändert. Beim ersten Start wird `/tmp/projekt42` als Standard gesetzt.

**Verzeichnisstruktur im Storage:**
```
{StorageBasePath}/
├── inbox/                  # Eingangsordner (überwacht)
├── YYYY/MM/DD/             # Fotos nach EXIF-Datum
├── nodate/YYYY/            # Fotos ohne EXIF-Datum
├── thumbnails/             # Generierte Thumbnails
└── duplicates/             # Erkannte Duplikate
```

---

## Bekannte Besonderheiten & Hinweise

### Kein Blazor
Das Projekt wurde ursprünglich mit Blazor Server gestartet, aber vollständig auf Razor Pages + Alpine.js migriert. Grund: Blazors Virtual-DOM-Diffing überschrieb bei jeder Navigation JS-gesetzte CSS-Klassen (z.B. den eingeklappten Sidebar-State). Alle `*.razor`-Dateien wurden entfernt.

### Alpine.js im Admin-Layout
`_AdminLayout.cshtml` bindet Alpine.js **nicht** ein. Admin-Seiten verwenden ausschließlich plain JavaScript. Kein `x-data`, `@click` etc. in Admin-Pages.

### Alpine.js-Direktiven in Razor-Views
`@click`, `:class` etc. kollidieren mit Razor's `@`-Syntax. Lösung: HTML als `System.Text.StringBuilder` aufbauen und mit `@Html.Raw()` ausgeben (verwendet im Sidebar-Folder-Tree in `_Layout.cshtml`).

### PostgreSQL DateTime
PostgreSQL `timestamp with time zone` erfordert `DateTimeKind.Utc`. EXIF-Daten liefern `Kind=Unspecified` → vor dem Speichern `DateTime.SpecifyKind(value, DateTimeKind.Utc)` anwenden.

### HEIC-Thumbnails
`ThumbnailService` überspringt HEIC/HEIF-Dateien (ImageSharp unterstützt das Format nicht nativ). Das Bild wird korrekt gespeichert, hat aber keinen Thumbnail.

### Upload-Größenlimit
Standard-Kestrel-Limit (30 MB) ist für Foto-Batches zu klein. Die Upload-Page hat `[RequestSizeLimit(1_073_741_824)]` und `[RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824)]` (je 1 GB).

### Doppelte Queue-Einträge
Beim Upload via Weboberfläche wird die Datei **direkt in die Queue** eingereiht (mit `topicId`). Der `FileWatcherService` sieht dieselbe Datei und reiht sie **ein zweites Mal** ein (mit `topicId = null`). Der zweite Verarbeitungsversuch schlägt mit "File no longer exists" fehl — das ist harmlos und erwartet.

### In-Process Queue
`IProcessingQueue` ist als `Singleton` registriert und verwendet `System.Threading.Channels`. Bei einem App-Neustart gehen alle noch nicht verarbeiteten Queue-Einträge verloren. Der `FileWatcherService` gleicht das beim Start aus, indem er alle Dateien im Inbox-Ordner neu einreiht.
