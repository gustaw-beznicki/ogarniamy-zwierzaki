# OCR feasibility (Phase 1)

Status: **PENDING owner review.** No real photo has been probed yet.

## Purpose

Check the product's largest assumption before the reading pipeline is built: that Azure Document Intelligence
`prebuilt-read` (API `2024-11-30`) recovers enough topic-bearing text from real veterinary documents for later search.
A successful HTTP response is not success. The question is whether the recovered text is materially useful and
whether gaps could mislead search.

## How to run the probe

Prerequisites: the Document Intelligence account exists and the owner's Entra user holds Cognitive Services User on
it (see `infra-rollout.md`, step 2, `developerPrincipalIds`).

```bash
az login
dotnet user-secrets set DocumentReading:Auth DeveloperCredential --project services/api
dotnet user-secrets set DocumentReading:Endpoint "https://<account>.cognitiveservices.azure.com/" --project services/api

# 1. Synthetic fixture (fictional content, safe to discuss)
dotnet run --project services/api -- ocr-probe --print-text services/api.Tests/Fixtures/Reading/synthetic-note.png

# 2. Five owner-selected real photos, kept OUTSIDE the repository
dotnet run --project services/api -- ocr-probe --print-text <path>/photo-1.jpg ... <path>/photo-5.jpg
```

Rules for the real-photo run:

- Keep the photos outside the repository. Do not commit photos, extracted text, filenames or medical details.
- `--print-text` prints to the terminal only. Compare it with the photo, then clear the terminal scrollback.
- Record only the sanitized judgements in the table below.
- Do not upload anything for training, labelling or feedback. The probe only calls analyze, get result and delete result.

## Provider assurances (references)

- Data, privacy and security for Document Intelligence:
  https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/document-intelligence/data-privacy-security
  - Customer documents and results are not used to train or improve Microsoft's models.
  - Input and analysis results are kept temporarily, for up to 24 hours, so the result can be retrieved.
- Early deletion through Delete Analyze Result:
  https://learn.microsoft.com/en-us/rest/api/aiservices/document-models/delete-analyze-result?view=rest-aiservices-v4.0+(2024-11-30)
  - The probe calls it for every result id it receives, whether the analysis succeeded or failed. A `204` means the
    request was accepted. It does not prove physical erasure.
- A submission whose response is lost (timeout) cannot be deleted early, because its result id is unknown. It falls
  back to the provider's bounded retention. The probe never resubmits.

## Results

Characteristics: print, stamp, handwriting, abbreviations, low light, skew, glare, multi-page and similar.
Recoverable: `yes` / `partial` / `no`. Misleading omissions: whether something missing could make search return a
wrong or incomplete answer, for example a missed drug name or date. Delete ack: `204` or the reported status.

| # | Characteristics | Topic-bearing terms recoverable? | Misleading omissions? | Delete ack | Original unchanged |
| --- | --- | --- | --- | --- | --- |
| Synthetic | print | PENDING | PENDING | PENDING | PENDING |
| 1 | PENDING | PENDING | PENDING | PENDING | PENDING |
| 2 | PENDING | PENDING | PENDING | PENDING | PENDING |
| 3 | PENDING | PENDING | PENDING | PENDING | PENDING |
| 4 | PENDING | PENDING | PENDING | PENDING | PENDING |
| 5 | PENDING | PENDING | PENDING | PENDING | PENDING |

The five photos should together cover representative print, stamps, handwriting and abbreviations.

## Verdict

**PENDING owner review.**

- Acceptance needs materially useful topic-bearing text from the real photos. If they fail, acceptance is blocked
  and the plan must be revisited. Success is not redefined as a successful API response.
- Verdict: _accept / blocked_. Date: _YYYY-MM-DD_. Notes (sanitized): _..._
