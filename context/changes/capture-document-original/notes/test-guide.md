# Post-deployment test guide

Site: https://mango-bush-08ef40b0f.5.azurestaticapps.net (PL) and `/en/` (EN).
Use test accounts and test files only — no real veterinary records.

**Prepare**
- Two test accounts, **A** and **B**, each with at least one animal. Give A two animals.
- A small PDF, 3–4 ordinary photos, a `.heic` photo (e.g. from an iPhone or downloaded) and an 11 MB file (`head -c 11000000 /dev/urandom > big.jpg`).

## 0. Deployment (5.2)

- [ ] The `deploy` workflow run is green, including the smoke test step.

## 1. Phone capture (4.3, 5.4)

On your phone, signed in as **A**:

- [ ] Add → **Take photo** opens the camera. Take 2 photos, then **Add photos** from the gallery for 1–2 more.
- [ ] Reorder them with ↑/↓ and remove one with ✕.
- [ ] Pick the second animal and an old date. Save without typing anything else.
- [ ] You see progress, then the document opens. Photos appear in the order you set.
- [ ] Open each photo and check it's the right one.

## 2. Desktop PDF, both languages (4.3, 4.4)

On a computer, signed in as **A**:

- [ ] Add → **Choose PDF** → Save. **Open PDF** and **Download** both work.
- [ ] The document page shows **Event date** and **Uploaded on** as two separate dates.
- [ ] Switch PL ↔ EN on the document page: the same document stays open.
- [ ] Sign out, sign in again, go Animals → animal → Documents: both documents are there and open.
- [ ] Open Add again: the animal you used last is preselected (on phone and on computer).

## 3. Errors (4.5)

- [ ] The date picker doesn't let you choose tomorrow.
- [ ] Adding the `.heic` photo shows a message about converting to JPEG/PNG or PDF.
- [ ] Adding an 11th photo shows "at most 10 photos".
- [ ] Adding the 11 MB file shows a "too large" message.
- [ ] Phone: start saving 3–4 photos, switch on airplane mode mid-upload, switch it off, tap **Try again**. The document saves, and the animal's list shows it **once**.

## 4. Privacy (5.3)

- [ ] Copy the address of one of A's photos (right-click → copy image address). Paste it in a private window: you get an error, not the photo.
- [ ] Sign in as **B** and paste the same address: again an error, not the photo. B's animal list shows none of A's documents.
- [ ] B's Add page preselects B's own animal, not A's.

## 5. After an API restart (5.3)

- [ ] Run `az webapp restart --resource-group rg-ogarniamy-mvp --name "$(az webapp list --resource-group rg-ogarniamy-mvp --query '[0].name' -o tsv)"`, wait a minute, sign in as A and open every original again.

## 6. Paperwork (5.3, 5.5)

- [ ] Skim [recover-originals.md](../../../../docs/sop/recover-originals.md) and say whether it makes sense to you.
- [ ] Optional: I can run the read-only storage settings check for you (public access off, versioning and 30-day soft delete on).

## Report back

Tell me "all OK", or list the boxes that failed with what you saw. Screenshots help.
