# CMS Event Service

.NET 9 take-home project that ingests CMS webhook batches, stores latest entity versions in a relational database, and exposes authenticated read endpoints.

- `CmsEventService`: ASP.NET Core Web API
- `CmsEventService.Tests`: xUnit integration and processing tests

## Requirements

- .NET 9 SDK or newer
- Windows, macOS, or Linux

## Run

```powershell
dotnet restore .\CmsEventService.slnx
dotnet run --project .\CmsEventService\CmsEventService.csproj
```

The API creates a local SQLite database named `cms-events.db` on startup.

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

## API

Ingest CMS events:

```powershell
$pair = "cmsPipeline01:3e8dc83f-4b8f-48c7-a2d4-44f89f476b65"
$auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pair))

Invoke-RestMethod http://localhost:5000/cms/events `
  -Method Post `
  -Headers @{ Authorization = "Basic $auth" } `
  -ContentType "application/json" `
  -Body '[
    { "type": "publish", "id": "X", "payload": { "title": "Hello" }, "version": 1, "timestamp": "2024-01-01T00:00:00Z" },
    { "type": "unPublish", "id": "Z", "payload": { "title": "Hidden" }, "version": 4, "timestamp": "2024-01-02T00:00:00Z" },
    { "type": "delete", "id": "Y", "timestamp": "2024-01-03T00:00:00Z" }
  ]'
```

List entities:

```powershell
$pair = "entityReader1:8e221201-a1cd-4f57-89c7-04d517651625"
$auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pair))
Invoke-RestMethod http://localhost:5000/entities -Headers @{ Authorization = "Basic $auth" }
```

Disable an entity locally as admin:

```powershell
$pair = "entityAdmin01:4f21956d-918a-4199-9787-e4bf9956363c"
$auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pair))
Invoke-RestMethod http://localhost:5000/entities/X/disabled `
  -Method Patch `
  -Headers @{ Authorization = "Basic $auth" } `
  -ContentType "application/json" `
  -Body '{ "disabled": true }'
```

## Design Notes

- Webhook ingestion is authenticated separately from consumer API access.
- `publish` upserts the latest published payload/version.
- `unPublish` upserts the latest payload/version and marks the entity unavailable to normal users, which handles the corner case where version `X+1` is unpublished before ever being publicly published.
- `delete` hard-deletes persisted entity data.
- Admin users see both available and disabled entities; normal users only see CMS-published entities that were not locally disabled.
- Admin local disable is an API-side override and does not affect CMS state.
- Processing is synchronous inside a database transaction. For a take-home service this favors simple correctness and deterministic responses; the processor is isolated so it can be moved behind a queue/background worker if webhook latency or throughput requirements grow.
- EF reads use `AsNoTracking`, projection, ordering, and a separate read context factory. SQLite is used as the relational database for platform-neutral local execution.
- Processed and failed events are logged both through structured application logs and the `EventLogs` table.

## Tests

```powershell
dotnet test .\CmsEventService.slnx
```

Test coverage includes event processing, delete/unpublish constraints, failed event logging, and valid/invalid Basic Authentication combinations.
