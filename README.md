# CMS Event Service

A .NET 9 project that ingests CMS webhook batches, stores latest entity versions in a relational database, and exposes authenticated read endpoints.

- `CmsEventService`: ASP.NET Core Web API
- `CmsEventService.Tests`: xUnit integration and processing tests

## Requirements

- .NET 9 SDK or newer
- Windows, macOS, or Linux

## Run

```powershell
dotnet restore .\CmsEventService.slnx
dotnet run --project .\CmsEventService\CmsEventService.csproj --no-restore
```

The API reads credentials and database settings from `CmsEventService/appsettings.json`, and creates a local SQLite database named `cms-events.db` on startup.

Swagger UI is available at:

```text
http://localhost:5058/swagger
```

Health check:

```text
http://localhost:5058/health
```

## Credentials

CMS webhook credentials:

- Username: `cmsPipeline01`
- Password: `3e8dc83f-4b8f-48c7-a2d4-44f89f476b65`

Normal API user:

- Username: `entityReader1`
- Password: `8e221201-a1cd-4f57-89c7-04d517651625`

Admin API user:

- Username: `entityAdmin01`
- Password: `4f21956d-918a-4199-9787-e4bf9956363c`

## API Usage

You can test the API either with PowerShell or through Swagger at `http://localhost:5058/swagger`.

### PowerShell

Create Basic Auth headers:

```powershell
$baseUrl = "http://localhost:5058"

$cmsPair = "cmsPipeline01:3e8dc83f-4b8f-48c7-a2d4-44f89f476b65"
$cmsAuth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($cmsPair))
$cmsHeaders = @{ Authorization = "Basic $cmsAuth" }

$readerPair = "entityReader1:8e221201-a1cd-4f57-89c7-04d517651625"
$readerAuth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($readerPair))
$readerHeaders = @{ Authorization = "Basic $readerAuth" }

$adminPair = "entityAdmin01:4f21956d-918a-4199-9787-e4bf9956363c"
$adminAuth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($adminPair))
$adminHeaders = @{ Authorization = "Basic $adminAuth" }
```

Check health:

```powershell
Invoke-RestMethod "$baseUrl/health"
```

Ingest CMS events:

```powershell
Invoke-RestMethod "$baseUrl/cms/events" `
  -Method Post `
  -Headers $cmsHeaders `
  -ContentType "application/json" `
  -Body '[
    {
      "type": "publish",
      "id": "article-1",
      "payload": { "title": "Public article", "body": "Visible to normal users" },
      "version": 1,
      "timestamp": "2024-01-01T00:00:00Z"
    },
    {
      "type": "unPublish",
      "id": "article-2",
      "payload": { "title": "Unpublished article", "body": "Admin-only latest data" },
      "version": 2,
      "timestamp": "2024-01-02T00:00:00Z"
    }
  ]'
```

List entities as a normal user:

```powershell
Invoke-RestMethod "$baseUrl/entities?page=1&pageSize=50" -Headers $readerHeaders
```

Expected: the normal user only sees CMS-published and locally enabled entities.

List entities as admin:

```powershell
Invoke-RestMethod "$baseUrl/entities?page=1&pageSize=50" -Headers $adminHeaders
```

Expected: the admin also sees CMS-unpublished and locally disabled entities.

The list endpoint returns:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 50,
  "totalItems": 0,
  "totalPages": 0
}
```

Disable an entity locally as admin:

```powershell
Invoke-RestMethod "$baseUrl/entities/article-1/disabled" `
  -Method Patch `
  -Headers $adminHeaders `
  -ContentType "application/json" `
  -Body '{ "disabled": true }'
```

Delete an entity from the CMS pipeline:

```powershell
Invoke-RestMethod "$baseUrl/cms/events" `
  -Method Post `
  -Headers $cmsHeaders `
  -ContentType "application/json" `
  -Body '[
    {
      "type": "delete",
      "id": "article-1",
      "timestamp": "2024-01-03T00:00:00Z"
    }
  ]'
```

### Swagger

1. Start the API and open `http://localhost:5058/swagger`.
2. Click `Authorize`.
3. Use CMS credentials before calling `POST /cms/events`.
4. Use reader credentials before calling `GET /entities` as a normal user.
5. Use admin credentials before calling `GET /entities` or `PATCH /entities/{id}/disabled` as an admin.

Swagger uses the same Basic Auth users listed in the credentials section.

## Design Notes

- Webhook ingestion is authenticated separately from consumer API access.
- `publish` upserts the latest published payload/version.
- `unPublish` upserts the latest payload/version and marks the entity unavailable to normal users, which handles the corner case where version `X+1` is unpublished before ever being publicly published.
- `delete` hard-deletes persisted entity data.
- Admin users see both available and disabled entities; normal users only see CMS-published entities that were not locally disabled.
- Admin local disable is an API-side override and does not affect CMS state.
- Processing is synchronous inside a database transaction. For a take-home service this favors simple correctness and deterministic responses; the processor is isolated so it can be moved behind a queue/background worker if webhook latency or throughput requirements grow.
- EF reads use `AsNoTracking`, projection, ordering, and a separate read context factory. SQLite is used as the relational database for platform-neutral local execution.
- `GET /entities` is paginated with `page` and `pageSize`; `pageSize` is capped at 100.
- Valid CMS events are fingerprinted from `type`, `id`, `version`, and `timestamp`. Exact duplicate retries are logged as `IgnoredDuplicate` and are not applied again.
- Stale events are logged as `IgnoredStale`.
- Processed, ignored, and failed events are logged both through structured application logs and the `EventLogs` table.
- Credentials are configured in `appsettings.json` for easy local setup and can still be overridden by environment variables or deployment-specific configuration providers.

## Tests

```powershell
dotnet test .\CmsEventService.slnx
```

Test coverage includes event processing, delete/unpublish constraints, duplicate-event handling, pagination, health checks, failed event logging, and valid/invalid Basic Authentication combinations.
