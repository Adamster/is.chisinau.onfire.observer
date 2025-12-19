# Agent Guidelines (Telegram Bot Minimal API)

## Scope
These guidelines apply to all files under `telegram-bot/`.

## Project Expectations
- Target framework: **.NET 10** (C#).
- Hosting model: **Minimal API** (single entry in `Program.cs`).
- Deployment target: **Azure App Service**.

## Configuration & Secrets
- Use the **Options** pattern with `IOptions<T>`.
- Bind configuration from `appsettings.json` and environment variables.
- Never hardcode secrets. Use environment variables in Azure App Service.
- Prefer `ValidateDataAnnotations()` or custom validation for required values.

## Code Style
- Keep `Program.cs` focused on composition (DI registration, middleware, endpoints).
- Move complex logic into dedicated services/classes.
- Prefer `sealed` classes for options and simple services.
- Use `ILogger<T>` for logging.
- Use `HttpClient` via `IHttpClientFactory`.

## API Conventions
- Provide a lightweight health endpoint (`/healthz`).
- Return typed results (`Results.Ok`, `Results.BadRequest`, etc.).
- Use explicit route names when helpful for diagnostics.

## Reliability
- Ensure idempotency for external side effects (Telegram sends, Supabase writes).
- Add retry policies for network calls where appropriate.

## Testing
- Keep tests deterministic; mock external calls.
- Avoid network access in unit tests.
