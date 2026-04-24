# Delivery Note OCR — POC

Proof-of-concept for the system described in `Delivery_Note_OCR_Project_Plan_v0.2.docx`:
scan a Lieferschein PDF, extract the four mandatory fields (delivery note no.,
project number, date, assignee) plus optional context fields using Azure Document
Intelligence, review/correct in a web UI, and persist an audited record plus a
training label for the learning loop.

This POC runs **entirely locally** (SQLite + file-system blob storage). The only
cloud dependency is Azure Document Intelligence.

## Layout

```
OCR POC/
├── DeliveryNoteOcr.sln
├── src/DeliveryNoteOcr.Api/    # .NET 8 Web API (SQLite, EF Core)
└── web/                        # React + TypeScript + Vite frontend
```

## Prerequisites

- **.NET 8 SDK** (https://dotnet.microsoft.com/download/dotnet/8.0). Your machine
  currently only has the .NET 6 runtime — install the 8 SDK before `dotnet build`.
- **Node 20+ and npm** (you have Node 24).
- An Azure Document Intelligence resource (endpoint + API key).

## Configure the Azure key

Local dev values live in `src/DeliveryNoteOcr.Api/appsettings.Development.json`,
which is git-ignored. The file is prewired with the endpoint/key you supplied.
Rotate the key in the Azure portal if you shared it somewhere public.

For anything shared, prefer user-secrets:

```
cd src/DeliveryNoteOcr.Api
dotnet user-secrets set "DocumentIntelligence:Endpoint" "https://..."
dotnet user-secrets set "DocumentIntelligence:ApiKey"   "..."
```

## Run the backend

```
cd src/DeliveryNoteOcr.Api
dotnet restore
dotnet run
```

The API starts on `http://localhost:5080` with Swagger at `/swagger`. On first
run it creates `runtime/app.db` (SQLite) and `runtime/blobs/` (uploaded PDFs).

## Run the frontend

```
cd web
npm install
npm run dev
```

Open http://localhost:5173. Vite proxies `/api/*` and `/health` to the backend.

## End-to-end flow

1. **Upload** a PDF on the Upload page (or drag-and-drop).
2. The backend stores the PDF locally, sends it to Azure Document Intelligence,
   and normalises the response into extracted fields + per-field confidence.
3. The Review page shows the PDF alongside editable fields with confidence badges.
4. **Save** preserves edits and writes an `AuditEvent` with a field-level diff.
5. **Confirm** transitions the note to `Confirmed`, writes another audit row,
   and emits a `TrainingLabel` (DB + `data/training/<labelId>/labels.json + ocr.json`).
6. The **Training** page shows the label backlog and lets an admin queue a run
   once enough corrections have accumulated.

## Models

Out of the box the extractor uses `prebuilt-layout` + regex heuristics for the
four core fields (project number `PR\d{2}-\d{3,5}`, Austrian/German date formats,
"Lieferschein Nr." proximity). That gets you a working loop immediately.

Once you've labelled ~30 of the 22 sample PDFs in Document Intelligence Studio
and trained a custom neural model, set:

```json
"DocumentIntelligence": {
  "CustomModelId": "<your-custom-model-id>"
}
```

The extractor will switch to the named-field code path and populate each field
with its real Azure confidence.

## Learning-loop scope in the POC

Implemented:

- Confirmed notes write a `TrainingLabel` row **and** a local `labels.json + ocr.json`
  artifact pair per example (in `runtime/training/<labelId>/`).
- `/api/training/status` reports backlog vs. configurable threshold.
- `/api/training/trigger` creates a `TrainingRun` row and associates pending labels.

Not yet implemented (next step when you move to real retraining):

- Uploading the per-label artifact pairs to an Azure Blob training container in
  the shape Document Intelligence expects (prefix per document: `*.pdf`,
  `*.pdf.ocr.json`, `*.pdf.labels.json`, plus a top-level `fields.json`).
- Calling `DocumentIntelligenceAdministrationClient.BuildClassifier`/`BuildModel`
  with that container as the source.
- Running the new model over a held-out test set and auto-promoting based on
  per-field F1.

The entity model and API surface are already shaped for those steps.

## Seed the Companies list

Add the known assignee companies via the Companies page (or `POST /api/companies`).
The extractor fuzzy-matches the handwritten assignee text against `name` and
`aliases`, so adding common misspellings/short forms ("Zimmel" for "Zimmel Transporte")
meaningfully improves auto-resolution.

## Test data

22 sample PDFs live in `../OCR/TestData/` alongside this POC folder — upload a
handful to exercise the loop.

## Current limitations (intentional POC scope)

- No auth: the backend runs as "dev-user"; an `X-User-Id` header overrides the
  actor for audit purposes. Entra ID integration is out of scope for the POC.
- Extraction runs synchronously inside the upload request. Fine for 22 PDFs;
  for production it would move behind Service Bus + a worker (per plan §4).
- Retraining is stubbed (queue + status only); see above.
- No Docker compose / infra-as-code yet.
