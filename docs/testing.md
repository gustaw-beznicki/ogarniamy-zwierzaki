# Tests and checks

```bash
dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj   # needs Docker running
dotnet build services/api/ogarniamy-zwierzaki-api.csproj
npm run check --prefix apps/web
npm run build --prefix apps/web
infra/deploy.sh lint                                                  # needs the Azure CLI with Bicep
```

The tests boot the real API against throwaway PostgreSQL 17 and Azurite containers (the Azurite image is pinned to the version in `compose.yaml`). Each test class gets its own containers; startup applies the normal migrations and creates the private originals container, and the production Blob Storage adapter is used. No local database, emulator or Azure credentials are involved. Pull-request CI runs all of the above plus an infrastructure `what-if`.

## What the API tests cover

- Accounts, sessions, animals and isolation between accounts: anonymous requests get 401 without a redirect, and another account's animals, documents, upload operations and originals get 404.
- Capture rules: one PDF (any number of internal pages) or 1–10 JPEG/PNG images; 10,485,760 bytes accepted and one byte more refused; empty files, unsupported formats, mismatched bytes, malformed dates and unknown time zones refused; today and earlier accepted and tomorrow refused, in time zones on both sides of UTC midnight.
- Antiforgery: every capture mutation without a valid token for the signed-in account is refused before anything changes.
- Originals: returned byte for byte in their confirmed order with private, non-cacheable headers and byte ranges, after an API restart and a fresh sign-in, with no content-processing service.
- Retries and failures: repeated and concurrent create, upload and completion produce one document; failures are injected at the storage adapter (`FaultyOriginalStorage`) and the EF Core boundary (`InjectedDatabaseFaults`) to cover a blob written before a database failure, a lost storage response, a lost completion response, a storage outage during upload, completion and reading, and a missing blob. Each answers with a retryable 503 and keeps the records.
- Logs: capture logs carry IDs and failure codes, never file names or contents.

Test files are synthetic: `SampleOriginals` holds small well-formed PNG, JPEG and multi-page PDF files and `TestOriginal` generates bodies of exact sizes. Never use real veterinary records or production credentials in tests.

The frontend has type and build checks only; there is no frontend test framework.
