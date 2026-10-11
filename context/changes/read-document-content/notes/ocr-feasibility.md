# OCR feasibility (Phase 1)

Status: **ACCEPTED by the owner on 2026-10-11** (accepted for the MVP, with the handwriting limitation below).

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
dotnet run --project services/api -- ocr-probe --print-text "$PWD/services/api.Tests/Fixtures/Reading/synthetic-note.png"

# 2. Five owner-selected real photos, kept OUTSIDE the repository (absolute paths: dotnet run starts in services/api)
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
| Synthetic | print | yes (2026-10-11: 1 page, 29 words, avg confidence 0.991, 2.8 s) | no | 204 | yes (sha256 match) |
| 1 | print, phone photo, shadow, bleed-through from the sheet below (1 page, 133 words, avg conf 0.970) | yes: all printed terms (drugs, dosing, dates) | no; one short noise line from bleed-through | 204 | yes |
| 2 | print, strong skew, shadow, stamp over signature (1 page, 170 words, avg conf 0.956) | yes: drugs, procedures, dates; one letter wrong in the stamp address | no | 204 | yes |
| 3 | print, round stamp over the header, rectangular stamp, short handwritten note (1 page, 384 words, avg conf 0.967) | yes: printed text including medical terms; handwritten note recovered with minor spelling errors | minor: one dose line detached from its drug; stamp text partly garbled | 204 | yes |
| 4 | print, stamp and signature over a table, skew, shadow (1 page, 132 words, avg conf 0.914) | yes: certificate type, vaccine name, series, vaccination and next-due dates | partial: vaccine expiry date and the vet's name under the stamp are lost or wrong | 204 | yes |
| 5 | pre-printed form filled in by hand, abbreviations, checkboxes, stamps (1 page, 144 words, avg conf 0.845) | partial: printed form labels and numbers yes; handwritten diagnosis only partly (two of three findings lost), names garbled | yes: lost handwritten findings; per-eye values out of order; printed checkbox labels for other species could match unrelated queries | 204 | yes |

The five photos should together cover representative print, stamps, handwriting and abbreviations.

## Verdict

- Acceptance needs materially useful topic-bearing text from the real photos. If they fail, acceptance is blocked
  and the plan must be revisited. Success is not redefined as a successful API response.
- Verdict: **accept** (sufficient for the MVP). Date: 2026-10-11. Account: `di-ogarniamy-2mnni3xfesmhm` (S0,
  swedencentral), created by the owner with `infra/bootstrap/document-reading-access.bicep` after reviewing its what-if.
- Notes (sanitized):
  - Printed documents read very well, including under stamps, strong skew, shadow and bleed-through (photos 1-4).
    Short handwritten notes next to print are recovered with minor spelling errors.
  - Known limitation: a doctor's handwriting on a pre-printed form (photo 5) is recovered only partly. Two of three
    handwritten findings were lost and names were garbled, so such findings may not be searchable. The original stays
    openable, which is the fallback the product relies on.
  - Text under a stamp can be lost or wrong (photo 4: a secondary date and a name).
  - Reading order on forms can detach values from their labels (per-eye values, a dose line).
  - Risk for S-04: printed form labels (for example checkbox options for other species) become document text and
    could match unrelated queries.
  - The owner also reviewed the S0 pay-per-use cost, the provider assurances above and the RBAC scope (Cognitive
    Services User on this account only, for the API identity and the owner). B1 and network review were dropped by
    the plan amendment of 2026-10-06.
