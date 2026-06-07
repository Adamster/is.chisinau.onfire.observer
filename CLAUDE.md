# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Telegram bot that monitors Chișinău fire incidents by polling RSS feeds, presenting candidates for human approval, and persisting approved incidents to Supabase. Deployed as an ASP.NET Core Minimal API on Azure App Service.

## Commands

All commands run from the `telegram-bot/` directory:

```bash
dotnet build                      # build
dotnet test                       # run all tests
dotnet test --filter "Name~Rss"   # run a single test class
dotnet run                        # run locally
```

## Architecture

### Data Flow

1. `RssPollingService` (BackgroundService) polls configured RSS feeds via `IRssFetcher` at a configurable interval
2. RSS items matching `FireKeywords` AND `CityKeywords` become `RssItemCandidate` entries stored in `IncidentCandidateStore`
3. Each new candidate is sent to the configured Telegram chat via `ITelegramNotifier` with Approve/Reject inline buttons
4. When a Telegram user interacts, the webhook at `POST /telegram/update` routes to `TelegramWebhookHandler`
5. On Approve: `ArticleDetailsFetcher` scrapes the article for street names, presents a street-selection keyboard
6. After street selection: `SupabaseIncidentRepository` inserts a `FireIncident` row via the Supabase C# SDK
7. `ApprovalProcessingService` (BackgroundService) is a fallback that persists any approved incidents that slipped past the inline flow

### In-Memory State Machine (`IncidentCandidateStore`)

`IncidentCandidateStore` is the central in-memory store (singleton). Each `PendingIncident` transitions through states: `Pending → Approved/Rejected → (street selected) → Persisting → Persisted`. All mutations on `PendingIncident` are guarded by `Try*` methods that enforce the valid state transitions. `IncidentCandidateStore` uses `ConcurrentDictionary` but individual `PendingIncident` state transitions are not thread-safe between concurrent callers — the `TryBeginPersisting` guard prevents double-writes.

### Callback Token System

Each candidate gets a short SHA-256-derived hex token. Tokens are embedded in Telegram inline keyboard callback data (format: `action:token[:payload]`) and resolved back to `candidateId` by `CallbackDataParser`. This indirection keeps callback data short and prevents guessing candidate IDs.

### Configuration (Options Pattern)

Three options classes bound in `Program.cs`:
- `TelegramBotOptions` (`"Telegram"` section): `BotToken`, `ChatId`, `WebhookUrl`, `Enabled`
- `RssOptions` (`"Rss"` section): `FeedUrls[]`, `PollIntervalSeconds`, `FireKeywords[]`, `CityKeywords[]`
- `SupabaseOptions` (`"Supabase"` section): supports either `ConnectionString` or `Url`+`ServiceRoleKey`

Secrets are provided via environment variables in Azure App Service, never hardcoded.

## Code Style

- `sealed` classes for options and services
- Extract long boolean conditions into named variables before `if` statements
- `IOptionsMonitor<T>` (not `IOptions<T>`) in background services to pick up config changes
- `HttpClient` via `IHttpClientFactory`
- `ILogger<T>` for all logging

## Testing

Tests use xUnit with Moq. All external calls (Telegram API, HTTP, Supabase) are mocked — no network access in tests. `TestOptionsMonitor<T>` is a local helper for injecting options into services under test.
