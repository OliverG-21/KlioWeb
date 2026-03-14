# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Projekt42 is a C# (.NET 8.0) console application, currently in its initial state.

## Commands

```bash
dotnet build    # Build the project
dotnet run      # Build and run the application
dotnet clean    # Remove build artifacts
```

## Architecture

Single-file console app (`Program.cs`) using top-level statements. No NuGet dependencies.

**Project config:**
- Target framework: `net8.0`
- Nullable reference types: enabled
- Implicit usings: enabled
