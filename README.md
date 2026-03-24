# Klio — Self-Hosted Photo Gallery

Selbst gehostete Foto-Galerie-Applikation für den Raspberry Pi. Fotos werden automatisch nach EXIF-Datum einsortiert, mit Thumbnails versehen und in einer PostgreSQL-Datenbank katalogisiert. Die Weboberfläche bietet eine öffentliche Galerie sowie einen passwortgeschützten Admin-Bereich.

---

## Inhaltsverzeichnis

- [Architektur](#architektur)
- [Projektstruktur](#projektstruktur)
- [Datenmodell](#datenmodell)
- [Verarbeitungs-Pipeline](#verarbeitungs-pipeline)
- [Web-Routen & Seiten](#web-routen--seiten)
- [Admin-Funktionen](#admin-funktionen)
- [Tagging-System](#tagging-system)
- [Ordner-Import](#ordner-import)
- [Technologie-Stack](#technologie-stack)
- [Docker-Setup](#docker-setup)
- [Lokale Entwicklung](#lokale-entwicklung)
- [Konfiguration](#konfiguration)
- [Deployment auf Raspberry Pi](#deployment-auf-raspberry-pi)
- [Bekannte Besonderheiten & Hinweise](#bekannte-besonderheiten--hinweise)

---

## Architektur

```
┌─────────────────────────────────────────────────────┐
│                   Klio.Web                      │
│  ASP.NET Core — Razor Pages + Vanilla JS            │
│                                                     │
│  Öffentlich: Mediathek, AlbumView, FolderView, Map  │
│  Admin:      Upload, Alben, Fotos, Einstellungen    │
│                                                     │
│  ┌──────────────────────────────────────────────────┐   │
│  │               Background Workers                │   │
│  │  FileWatcherService  │  ImageProcessorService   │   │
│  │                      │  TaggingProcessorService │   │
│  └──────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────┘
                         │ DI / Scoped Services
          ┌──────────────┴──────────────┐
          │                             │
┌─────────▼──────────┐      ┌───────────▼──────────────┐
│  Klio.Core    │      │ Klio.Infra            │
│  Models            │      │ EF Core Repositories       │
│  Interfaces        │      │ FileOrganizerService       │
└────────────────────┘      │ ExifService                │
                            │ ThumbnailService           │
                            │ VideoThumbnailService      │
                            │ OllamaTaggingService       │
                            │ HashService                │
                            │ ProcessingChannel          │
                            └───────┬───────┬────────────┘
                                    │       │
                        ┌───────────▼──┐ ┌──▼──────────────────┐
                        │  PostgreSQL  │ │  ffmpeg-service      │
                        │  (Docker)    │ │  (Docker, Port 5100) │
                        └─────────────┘ └─────────────────────┘
                                          ┌──────────────────────┐
                                          │  Ollama              │
                                          │  (lokal, Port 11434) │
                                          └──────────────────────┘
```

Die Applikation ist ein einzelner ASP.NET Core Host, der gleichzeitig den Webserver und die Background-Worker betreibt. Ein separates `Klio.Worker`-Projekt enthält die Worker-Klassen, wird aber als Abhängigkeit in `Klio.Web` eingebunden.

Der **ffmpeg-service** ist ein eigenständiger Python/Flask-Container, der für Video-Verarbeitung zuständig ist (Thumbnail-Extraktion, Transkodierung zu H.264 MP4). Er wird nur beim Video-Upload kontaktiert und kann ausfallen, ohne die Foto-Funktionalität zu beeinträchtigen.

---

## Projektstruktur

```
Klio/
├── docker-compose.yml          # PostgreSQL + ffmpeg-service
├── ffmpeg-service/             # Python/Flask Container für Video-Verarbeitung
│   ├── Dockerfile
│   └── app.py
├── global.json                 # .NET SDK Version (10.0.201)
├── Klio.sln
└── src/
    ├── Klio.Core/         # Domain — keine externen Abhängigkeiten
    │   ├── Models/             # Image, Topic, AlbumFolder, AppSettings, ImageTag, DuplicateLog
    │   └── Interfaces/         # IImageRepository, ITopicRepository, IImageTagRepository, ...
    │
    ├── Klio.Infrastructure/  # Datenzugriff & Services
    │   ├── Data/               # AppDbContext, Migrations
    │   ├── Repositories/       # EF Core Implementierungen der Interfaces
    │   └── Services/           # FileOrganizerService, ExifService, ThumbnailService,
    │                           #   OllamaTaggingService, ...
    │
    ├── Klio.Web/          # ASP.NET Core Host
    │   ├── Pages/              # Razor Pages (Gallery + Admin)
    │   │   ├── Admin/          # Passwortgeschützter Bereich
    │   │   ├── Api/            # Öffentliche API-Endpoints (z.B. /api/imagetags)
    │   │   ├── Images/         # Thumbnail- und Vollbild-Endpoints
    │   │   └── Shared/         # _Layout.cshtml, _AdminLayout.cshtml
    │   ├── wwwroot/            # Bootstrap, Leaflet, CSS, lightbox.js, leafletInterop.js
    │   └── Program.cs          # DI-Registrierung, Middleware, DB-Migration
    │
    └── Klio.Worker/       # BackgroundService-Klassen
        ├── FileWatcherService.cs
        ├── ImageProcessorService.cs
        └── TaggingProcessorService.cs
```

---

## Datenmodell

```
AlbumFolder (self-referencing)
├── Id, Name
├── ParentFolderId → AlbumFolder (nullable, Restrict on delete)
└── SubFolders, Albums (Navigation)
    Note: Ordner und Alben können auf jeder Ebene gemeinsam existieren

Topic (= Album)
├── Id, Name, Description
├── FolderId → AlbumFolder (nullable, SetNull on delete)
├── CoverImageId (nullable)
├── SortOrder
└── Images (Navigation)

Image
├── Id
├── OriginalFilename, StoredFilename, FilePath, ThumbnailPath
├── FileSizeBytes, Hash (SHA-256, unique index)
├── MimeType
├── IsVideo (bool, false für Fotos)
├── DateTaken (DateTime?, UTC, aus EXIF)
├── GpsLatitude, GpsLongitude (double?, aus EXIF)
├── CameraModel (string?, aus EXIF)
├── TopicId → Topic (nullable, SetNull on delete)
└── ProcessedAt, CreatedAt

DuplicateLog
├── IncomingFilename, DuplicatePath
├── ExistingImageId, ExistingImagePath
└── DetectedAt

ImageTag
├── Id
├── ImageId → Image (Cascade on delete)
├── Tag (string, lowercase)
├── Source (TagSource: Auto=0, Manual=1)
└── CreatedAt

AppSettings (singleton)
├── AdminPasswordHash (BCrypt)
├── StorageBasePath, InboxPath, FolderInboxPath
├── ThumbnailWidth, ThumbnailHeight
├── TaggingEnabled (bool)
├── OllamaUrl (default: http://localhost:11434)
├── OllamaModel (default: llava-phi3)
└── OllamaPrompt (deutsch, max 8 Tags als JSON-Array)
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
                          ├─ ExifService.ExtractAsync()        → DateTaken, GPS, CameraModel
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
                          ├─ IImageRepository.AddAsync()       → DB-Eintrag
                          │
                          └─ TaggingEnabled?
                               └─ ITaggingQueue.Enqueue(imageId) → TaggingProcessorService
                                        │
                                        ▼
                                  OllamaTaggingService.TagImageAsync()
                                        │
                                        ▼
                                  IImageTagRepository.AddRangeAsync()
```

**Wichtig:** Die `topicId` wird von der Queue bis zum DB-Eintrag durchgereicht. Fotos, die über den Inbox-Ordner (FileWatcher) eintreffen, erhalten `topicId = null`.

Der `TaggingEnabled`-Toggle steuert nur den auto-Enqueue beim Import. Manuelles Einreihen über `/admin/settings` und die Verarbeitung durch `TaggingProcessorService` funktionieren unabhängig davon.

---

## Web-Routen & Seiten

### Öffentlich

| Route | Seite | Beschreibung |
|-------|-------|--------------|
| `/` | Index | Mediathek als Grid, älteste zuerst, Jahrestrennlinie, Lightbox |
| `/album/{topicId:int}` | TopicView | Fotos eines Albums, sortierbar, Lightbox, Tag-Flyouts |
| `/folder/{folderId:int}` | FolderView | Alben innerhalb eines Ordners |
| `/map` | Map | Leaflet-Karte mit GPS-Markern (geclustert nach Position) |
| `/images/thumbnail/{id}` | Thumbnail | Thumbnail ausliefern |
| `/images/serve/{id}` | Serve | Vollbild ausliefern |
| `/api/imagetags` | ImageTags API | `GET ?imageId=X` — Tags eines Bildes als JSON |

Alle unbekannten Routen und Fehler werden auf `/` weitergeleitet.

### Admin (Cookie-Auth erforderlich)

| Route | Seite | Beschreibung |
|-------|-------|--------------|
| `/admin` | Dashboard | Übersicht |
| `/admin/login` | Login | Authentifizierung (Redirect auf ursprüngliche URL nach Login) |
| `/admin/alben` | Alben | Ordner- und Albumverwaltung (CRUD) |
| `/admin/album/{topicId:int}` | AlbumPhotos | Fotos im Album, verschieben, Upload-Link |
| `/admin/unassigned` | UnassignedPhotos | Nicht zugeordnete Fotos einem Album zuweisen |
| `/admin/upload` | Upload | Fotos hochladen, direkt Album zuordnen |
| `/admin/import` | Import | Ordnerstruktur / ZIP-Dateien als Batch importieren |
| `/admin/tagging` | Tagging | Manuelles Tagging, Tag-Verwaltung (Löschen, Zusammenführen) |
| `/admin/duplicates` | Duplicates | Duplikat-Log |
| `/admin/settings` | Settings | Passwort, Pfade, Thumbnail-Größe, Ollama-Konfiguration, EXIF-Reprocess, Clear-All |

---

## Admin-Funktionen

### Alben-Verwaltung (`/admin/alben`)
- Hierarchische Ordnerstruktur (unbegrenzte Tiefe) anlegen, umbenennen, verschieben, löschen
- Alben und Unterordner können auf jeder Ebene gemeinsam existieren (keine Leaf-Folder-Beschränkung)
- Alben erstellen, bearbeiten, löschen und zwischen beliebigen Ordnern verschieben
- Drag-and-drop Sortierung für Ordner und Alben

### Upload (`/admin/upload`)
- Mehrere Dateien gleichzeitig (Limit: 1 GB pro Request)
- Albumzuordnung beim Upload: kein Album / vorhandenes Album / neues Album anlegen
- Unterstützte Foto-Formate: JPEG, PNG, HEIC
- Unterstützte Video-Formate: MP4, WebM, MOV, M4V, AVI, MKV, OGV
  - MP4/WebM werden direkt gespeichert
  - Alle anderen Formate (MOV, AVI, MKV, …) werden automatisch zu H.264 MP4 transkodiert (erfordert ffmpeg-service)

### Foto-Verwaltung
- **Album-Fotos** (`/admin/album/{id}`): Fotos einzeln oder als Bulk in ein anderes Album verschieben, direkter Upload-Link mit vorausgewähltem Album
- **Nicht zugeordnet** (`/admin/unassigned`): Mediathek ohne Album-Zuordnung, einzeln oder bulk zuweisen

### Duplikat-Erkennung
Automatisch bei jeder Verarbeitung via SHA-256-Hash. Duplikate werden in `{storage}/duplicates/` verschoben und geloggt. Kein Fehler, kein Datenverlust.

### Einstellungen (`/admin/settings`)
- Passwort ändern
- Speicherpfade anpassen (inkl. FolderInboxPath)
- Thumbnail-Größe konfigurieren
- **Auto-Tagging:** Ollama-URL, Modell und Prompt konfigurieren (siehe [Tagging-System](#tagging-system))
- **EXIF neu einlesen:** Aktualisiert CameraModel und GPS für alle vorhandenen Bilder
- **Alle Daten löschen:** Entfernt alle Bilddateien und DB-Einträge (mit Bestätigungsdialog)

---

## Tagging-System

Bilder können manuell oder automatisch mit Tags versehen werden. Tags werden als Badges in der Lightbox und als Flyout auf Album-Thumbnails angezeigt.

### Manuelles Tagging (`/admin/tagging`)

- Alle Bilder durchblättern, Tags per Eingabefeld hinzufügen (mit Autovervollständigung aus vorhandenen Tags)
- Tags per Klick entfernen
- **Tag-Verwaltung:**
  - Tag global löschen (wird von allen Bildern entfernt)
  - Zwei Tags zusammenführen: alle Bilder des Quell-Tags erhalten den Ziel-Tag, Duplikate werden vermieden

### Automatisches Tagging (Ollama)

Das Auto-Tagging nutzt ein lokales [Ollama](https://ollama.ai)-Modell mit Vision-Unterstützung (z.B. `llava-phi3`).

**Konfiguration unter `/admin/settings`:**

| Einstellung | Standard | Beschreibung |
|-------------|----------|--------------|
| `OllamaUrl` | `http://localhost:11434` | URL des Ollama-Servers |
| `OllamaModel` | `llava-phi3` | Vision-Modell (muss auf dem Ollama-Server installiert sein) |
| `OllamaPrompt` | Deutsch, max. 8 Tags | Prompt-Text, der ans Modell gesendet wird |
| `TaggingEnabled` | `false` | Steuert Auto-Tagging beim Import neuer Bilder |

**Ablauf:**
1. Ollama lokal starten: `ollama serve` und Modell laden: `ollama pull llava-phi3`
2. Unter `/admin/settings` URL und Modell einstellen, Verbindung testen
3. Optional: "Neue Bilder automatisch taggen" aktivieren (auto-enqueue beim Import)
4. Manuell: "Alle ungetaggten Bilder einreihen" — startet Batch-Tagging unabhängig vom Toggle

**Wichtig:** Der "Neue Bilder automatisch taggen"-Toggle steuert **nur** den auto-Enqueue beim Import. Manuelles Einreihen und die Worker-Verarbeitung funktionieren immer.

**Standard-Prompt (deutsch):**
```
Liste bis zu 8 kurze beschreibende Tags für dieses Bild auf Deutsch.
Nutze einfache, allgemeine Begriffe wie Personen, Orte, Objekte, Aktivitäten oder Stimmungen.
Antworte nur mit einem JSON-Array, keine Erklärung.
Beispiel: ["person","draußen","sommer","lachen"]
```

### Tag-Anzeige

- **Lightbox:** Tags als Badges unter dem Bild (blau = manuell, grau = automatisch)
- **Album-Ansicht:** Tag-Icon auf Thumbnails mit Tags → Flyout zeigt alle Tags, Klick auf Tag filtert die Album-Ansicht

---

## Ordner-Import

### Konzept (`/admin/import`)

Der Ordner-Import ermöglicht es, eine bestehende Verzeichnisstruktur (z.B. aus einer externen Festplatte) inklusive Unterordner als Alben-Hierarchie zu importieren. Die Struktur des Dateisystems wird direkt auf das Alben-/Ordner-Modell abgebildet.

**Verzeichnis-Mapping:**
```
FolderInbox/
└── Urlaub 2024/                     → AlbumFolder "Urlaub 2024"
    ├── bild1.jpg                    → Album "Urlaub 2024" (gleicher Name wie Ordner)
    └── Strand/                      → AlbumFolder "Strand" (Kind von "Urlaub 2024")
          └── bild2.jpg              → Album "Strand"
```

**Regeln:**
- Jedes Verzeichnis → `AlbumFolder` mit gleichem Namen
- Jedes Verzeichnis, das direkt Bild-/Videodateien enthält → zusätzlich `Topic` (Album) gleichen Namens
- Bereits existierende Ordner/Alben gleichen Namens auf gleicher Ebene werden wiederverwendet (kein Duplikat)
- Dateien direkt im FolderInbox-Root (ohne Unterordner) → `topicId = null` (wie Inbox 1)

### Ablauf

1. Dateien/Ordner in den `FolderInboxPath`-Ordner legen (manuell oder via sftp)
2. `/admin/import` aufrufen — Inhalt wird aufgelistet
3. **„Import starten"** klicken → Ordner/Alben werden angelegt, Dateien in die Queue eingereiht
4. Der Worker verarbeitet die Dateien im Hintergrund (EXIF, Thumbnails, Storage-Ablage)
5. Optional: **„FolderInbox leeren"** um verarbeitete Dateien zu entfernen

### ZIP-Import

Eine `.zip`-Datei im FolderInbox-Ordner wird beim Import automatisch erkannt:
- Entpackt nach `{FolderInboxPath}/.tmp_{guid}/`
- Die entpackte Struktur wird wie ein normaler Ordner behandelt
- ZIP-Datei wird nach erfolgreichem Entpacken sofort gelöscht
- Verschachtelte ZIPs (ZIP-in-ZIP) werden ignoriert und als Hinweis angezeigt
- Das Temp-Verzeichnis bleibt bestehen bis der Worker alle Dateien verarbeitet hat

### Unterstützte Formate

| Typ | Formate |
|-----|---------|
| Bilder | JPG, JPEG, PNG, HEIC, HEIF |
| Videos (nativ) | MP4, WebM |

> Videos in anderen Formaten (MOV, AVI, …) müssen vor dem Import in MP4/WebM konvertiert werden. Der Upload-Bereich unterstützt diese Formate mit automatischer Transkodierung.

### FolderInboxPath konfigurieren

Der Pfad kann direkt auf der Import-Seite oben im Eingabefeld geändert werden, oder unter `/admin/settings`.

```
{StorageBasePath}/
├── inbox/              # Inbox 1 — automatisch vom FileWatcher überwacht
└── folder_inbox/       # Inbox 2 — manueller Ordner-Import (Standard)
```

---

## Galerie-Features

### Lightbox
- Geteilte Implementierung in `lightbox.js` + `_Layout.cshtml` (eine Instanz für alle Seiten)
- Navigation per Pfeiltasten (←/→) und Pfeil-Buttons links/rechts vom Bild (kein Loop)
- Anzeige: Aufnahmedatum, Kameramodell (aus EXIF), GPS-Ort (via Nominatim Reverse Geocoding)
- **Tags als Badges** unter dem Bild (blau = manuell, grau = automatisch); geladen via `/api/imagetags`
- **"Auf Karte anzeigen"**: erscheint wenn Bild GPS-Daten hat
- **"Zurück zur Karte"**: erscheint wenn Bild aus der Kartenansicht geöffnet wurde (stellt Position und Zoom wieder her)

### Zoom-Slider
- Verfügbar in "Mediathek" und in Album-Ansichten
- Standard 50% = 200px Thumbnail-Größe, Bereich 100px–400px (logarithmische Skala)
- Icons klickbar (±10 Schritte), Doppelklick auf Slider → Reset auf 50%

### Karte (`/map`)
- GPS-Marker werden nach Position geclustert (≈11m Radius)
- Einzelbild-Marker: öffnet Album + Lightbox mit "Zurück zur Karte"-Button
- Mehrfach-Marker: zeigt Anzahl-Badge, öffnet Album ohne automatische Lightbox
- Rücksprung zur Karte stellt exakte Position und Zoomstufe wieder her

### Videos
- Videos erscheinen zusammen mit Fotos in der Mediathek und in Alben
- Thumbnail wird beim Upload über den ffmpeg-Service generiert
- Wiedergabe direkt in der Lightbox (HTML5 `<video>`, Range-Requests für Seek)
- Play-Overlay-Icon auf Video-Thumbnails in der Galerie
- MOV/AVI/MKV werden beim Upload automatisch zu H.264 MP4 transkodiert

### Tag-Flyout (Album-Ansicht)
- Thumbnails mit Tags zeigen ein Tag-Icon in der Datum-Zeile
- Hover über das Icon öffnet ein Flyout mit allen Tags des Bildes
- Klick auf einen Tag übernimmt ihn in die Tag-Filtersuche der Album-Ansicht

### Sortierung
- **Mediathek**: fest aufsteigend (älteste zuerst), Jahrestrennlinien zwischen Jahren
- **Album**: umschaltbar (neueste/älteste zuerst), client-seitig

---

## Technologie-Stack

| Bereich | Technologie |
|---------|-------------|
| Framework | .NET 10, ASP.NET Core Razor Pages |
| Frontend | Vanilla JavaScript, Bootstrap 5, Bootstrap Icons |
| Karten | Leaflet.js (GPS-Ansicht, Marker-Popups) |
| Datenbank | PostgreSQL 16 via Entity Framework Core + Npgsql |
| Bildverarbeitung | SixLabors.ImageSharp (ARM-kompatibel) |
| Video-Verarbeitung | ffmpeg (Python/Flask Docker-Service, Port 5100) |
| EXIF/Metadaten | MetadataExtractor (Fotos + Videos) |
| Passwörter | BCrypt.Net |
| Queueing | System.Threading.Channels (in-process, kein externer Broker, zwei Queues: Processing + Tagging) |
| Auto-Tagging | Ollama (lokal, Vision-Modell wie llava-phi3) |
| Auth | Cookie-Authentifizierung (8h sliding expiration) |

---

## Docker-Setup

Das Projekt verwendet zwei Docker-Container, die über `docker-compose` verwaltet werden.

### Container

| Service | Image | Port | Beschreibung |
|---------|-------|------|--------------|
| `postgres` | `postgres:16-alpine` | 5432 | PostgreSQL-Datenbank |
| `ffmpeg-service` | lokales Build (`./ffmpeg-service`) | 5100 | Video-Verarbeitung (Thumbnails, Transkodierung) |

### Konfiguration des Storage-Pfads

Der ffmpeg-Container muss denselben Storage-Pfad sehen wie die .NET-App. Das Volume-Mapping in `docker-compose.yml` verwendet die Umgebungsvariable `STORAGE_PATH`:

```bash
# Standard (im docker-compose.yml voreingestellt):
STORAGE_PATH=/Users/oliver/Documents/Klio-Data

# Überschreiben beim Start:
STORAGE_PATH=/mnt/data/klio docker-compose up -d

# Oder dauerhaft in .env-Datei im Repo-Root:
echo "STORAGE_PATH=/mnt/data/klio" > .env
docker-compose up -d
```

> **Wichtig:** `STORAGE_PATH` muss mit dem `StorageBasePath` in den App-Einstellungen (`/admin/settings`) übereinstimmen, sonst kann der ffmpeg-Container die Video-Dateien nicht finden.

### Container starten

```bash
# Alle Container starten (beim ersten Mal: ffmpeg-Image wird gebaut)
docker-compose up -d

# Status prüfen
docker-compose ps

# Logs anzeigen
docker-compose logs ffmpeg-service
docker-compose logs postgres

# ffmpeg-Service Gesundheitscheck
curl http://localhost:5100/health
# → {"ok": true}
```

### Container neu bauen

Nach Änderungen an `ffmpeg-service/app.py` oder `ffmpeg-service/Dockerfile`:

```bash
docker-compose up -d --build ffmpeg-service
```

### ffmpeg-Service API

Der ffmpeg-Service stellt eine einfache HTTP-API bereit:

#### `GET /health`
Liefert `{"ok": true}` wenn der Dienst läuft.

#### `POST /thumbnail`
Extrahiert ein Standbild aus einem Video.

```json
{
  "input": "/storage/videos/2026/03/21/myvideo.mp4",
  "output": "/storage/thumbnails/myvideo_thumb.jpg",
  "timestamp": "00:00:01"
}
```

#### `POST /transcode`
Transkodiert ein Video zu H.264 MP4 (browser-kompatibel).

```json
{
  "input": "/storage/videos/2026/03/21/clip.mov",
  "output": "/storage/videos/2026/03/21/clip.mp4"
}
```

Alle Pfade sind **Container-interne Pfade** (beginnen mit `/storage/`). Die .NET-App übersetzt die host-absoluten Pfade automatisch anhand des `StorageBasePath`.

### Raspberry Pi / Produktion

Auf dem Raspberry Pi läuft die .NET-App direkt (kein Container), aber PostgreSQL und ffmpeg-service laufen als Docker-Container. Eine vollständige Schritt-für-Schritt-Anleitung findet sich im Abschnitt [Deployment auf Raspberry Pi](#deployment-auf-raspberry-pi).

---

## Lokale Entwicklung

### Voraussetzungen
- .NET 10 SDK (`global.json` pinnt auf `10.0.201`)
- Docker (für PostgreSQL und ffmpeg-service)

### Setup

```bash
# 1. Docker-Container starten (PostgreSQL + ffmpeg-service)
docker-compose up -d

# 2. Abhängigkeiten wiederherstellen
dotnet restore

# 3. Datenbank migrieren + App starten
cd src/Klio.Web
dotnet run
```

Die App startet auf `http://localhost:5005`. Die Datenbank wird beim ersten Start automatisch migriert und mit Standardwerten befüllt (Admin-Passwort: `admin`).

### Migrations

```bash
# Neue Migration anlegen
dotnet ef migrations add <Name> --project src/Klio.Infrastructure --startup-project src/Klio.Web

# Datenbank aktualisieren
dotnet ef database update --project src/Klio.Infrastructure --startup-project src/Klio.Web
```

### Standard-Konfiguration (AppSettings)

| Einstellung | Standardwert |
|-------------|-------------|
| `StorageBasePath` | `/tmp/klio` |
| `InboxPath` | `/tmp/klio/inbox` |
| `FolderInboxPath` | `/tmp/klio/folder_inbox` |
| `ThumbnailWidth` | 300 px |
| `ThumbnailHeight` | 300 px |
| `TaggingEnabled` | `false` |
| `OllamaUrl` | `http://localhost:11434` |
| `OllamaModel` | `llava-phi3` |
| Admin-Passwort | `admin` |

---

## Konfiguration

### Connection String

In `appsettings.json` oder als Umgebungsvariable:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=klio;Username=klio;Password=kliodev"
  }
}
```

### Speicherpfade

Werden in der Datenbank (`AppSettings`) gespeichert und über `/admin/settings` geändert. Beim ersten Start wird `/tmp/klio` als Standard gesetzt.

**Verzeichnisstruktur im Storage:**
```
{StorageBasePath}/
├── inbox/                  # Eingangsordner (überwacht vom FileWatcher)
├── folder_inbox/           # Eingangsordner für Ordner-Import (manuell)
├── YYYY/MM/DD/             # Fotos nach EXIF-Datum
├── nodate/YYYY/            # Fotos ohne EXIF-Datum
├── videos/YYYY/MM/DD/      # Videos nach Datum (H.264 MP4)
├── thumbnails/             # Thumbnails (Fotos + Videos)
└── duplicates/             # Erkannte Duplikate
```

---

## Deployment auf Raspberry Pi

Klio läuft auf dem Raspberry Pi als systemd-Service. PostgreSQL und der ffmpeg-Service laufen als Docker-Container.

### Voraussetzungen

```bash
# .NET 10 Runtime installieren (ARM64)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --runtime dotnet
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc && source ~/.bashrc

# Docker installieren
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
# Neu einloggen damit die Gruppe aktiv wird
```

### 1. Repository klonen

```bash
git clone https://github.com/OliverG-21/Klio.git
cd Klio
```

### 2. Storage-Verzeichnis anlegen

```bash
sudo mkdir -p /mnt/fotos
sudo chown $USER:$USER /mnt/fotos
```

### 3. Docker-Container konfigurieren und starten

```bash
# Storage-Pfad in .env setzen (muss mit AppSettings übereinstimmen)
echo "STORAGE_PATH=/mnt/fotos" > .env

# Container starten (PostgreSQL + ffmpeg-service)
docker compose up -d

# Prüfen ob alles läuft
docker compose ps
```

### 4. App bauen und veröffentlichen

**Option A — direkt auf dem Pi bauen** (braucht .NET SDK statt nur Runtime):

```bash
dotnet publish src/Klio.Web -c Release -r linux-arm64 --no-self-contained -o /opt/klio
```

**Option B — auf dem Mac bauen, auf den Pi übertragen** (empfohlen, schneller):

```bash
# Auf dem Mac:
dotnet publish src/Klio.Web -c Release -r linux-arm64 --no-self-contained -o ./publish

# Zum Pi übertragen:
rsync -av ./publish/ pi@RASPI_IP:/opt/klio/
```

### 5. Konfiguration

Verbindungsstring und Storage-Pfad auf dem Pi anpassen:

```bash
sudo nano /opt/klio/appsettings.json
```

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=klio;Username=klio;Password=SICHERES_PASSWORT"
  }
}
```

> Storage-Pfad und weitere Einstellungen werden beim ersten Start in der DB gesetzt und danach über `/admin/settings` verwaltet.

### 6. systemd-Service anlegen

```bash
sudo nano /etc/systemd/system/klio.service
```

```ini
[Unit]
Description=Klio Photo Gallery
After=network.target docker.service
Requires=docker.service

[Service]
Type=simple
User=pi
WorkingDirectory=/opt/klio
ExecStart=/home/pi/.dotnet/dotnet /opt/klio/Klio.Web.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5005

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable klio
sudo systemctl start klio

# Status prüfen
sudo systemctl status klio
sudo journalctl -u klio -f
```

### 7. Erster Start

Beim ersten Start:
- Datenbankmigrationen werden automatisch ausgeführt
- Standard-Einstellungen werden in der DB angelegt (Storage-Pfad: `/tmp/klio`)
- **Sofort** unter `/admin/settings` den Storage-Pfad auf `/mnt/fotos` ändern
- Admin-Passwort ändern (Standard: `admin`)

Die App ist dann erreichbar unter: `http://RASPI_IP:5005`

### Updates einspielen

```bash
# Auf dem Mac:
git pull
dotnet publish src/Klio.Web -c Release -r linux-arm64 --no-self-contained -o ./publish
rsync -av ./publish/ pi@RASPI_IP:/opt/klio/

# Auf dem Pi:
sudo systemctl restart klio
```

---

## Bekannte Besonderheiten & Hinweise

### Kein Blazor / Kein Alpine.js
Das Projekt wurde ursprünglich mit Blazor Server gestartet, aber vollständig auf Razor Pages migriert. Interaktivität wird durch Vanilla JavaScript umgesetzt. Alpine.js wird nicht mehr verwendet.

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

### EF Core DbContext Thread-Safety
`AppDbContext` ist nicht thread-safe. Parallele `Task.WhenAll`-Aufrufe auf demselben Context führen zu `ConcurrencyDetectorException`. Alle Repository-Aufrufe sequenziell awaiten.

### Video-Transkodierung
Die Transkodierung zu H.264 MP4 läuft synchron im Upload-Request und kann bei langen Videos mehrere Minuten dauern. Schlägt die Transkodierung fehl (ffmpeg-Service nicht erreichbar), wird das Original-Format als Fallback gespeichert — es ist dann möglicherweise nicht in allen Browsern abspielbar (z.B. HEVC-MOV nur in Safari).

### ffmpeg-Service Pfad-Übersetzung
`VideoThumbnailService` übersetzt host-absolute Pfade in Container-Pfade: `{StorageBasePath}/rest/of/path` → `/storage/rest/of/path`. Das funktioniert nur korrekt, wenn das Docker-Volume den richtigen Host-Pfad mounted (via `STORAGE_PATH`-Umgebungsvariable in `docker-compose.yml`).
