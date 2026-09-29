---
date: 2026-09-29T11:57:13+02:00
researcher: Claude (Opus 5.5) with Gustaw Beźnicki
git_commit: 98a1cd5
branch: main
repository: ogarniamy-zwierzaki
topic: "Authentication, authorization and password safety in the API compared with Microsoft Learn guidance"
tags: [research, security, authentication, authorization, aspnetcore-identity, data-protection, services-api]
status: complete
last_updated: 2026-09-29
last_updated_by: Claude (Opus 5.5)
last_updated_note: "Follow-up: compared with the OWASP Password Storage Cheat Sheet. The iteration count was raised to 220,000 with verified rehash on login; the pepper moved to the password-reset scope."
---

# Research: Authentication, authorization and password safety compared with Microsoft Learn guidance

**Date**: 2026-09-29T11:57:13+02:00
**Researcher**: Claude (Opus 5.5) with Gustaw Beźnicki
**Git Commit**: 98a1cd5 (local `main`; Phase 3 of this change, also on branch `account-and-first-animal-p3`, PR #6)
**Branch**: main
**Repository**: ogarniamy-zwierzaki

## Research Question

How does the API handle authentication and authorization? This covers ASP.NET Core Identity, the cookie session, password hashing and storage, lockout, the fallback authorization policy, data-protection keys and owner isolation. How does that compare with Microsoft Learn enterprise guidance? Focus: are passwords stored safely, and can they leak (database, logs, responses, telemetry, config, tests, git)?

## Summary

**Passwords are stored safely, as Microsoft's defaults intend, but with a work factor below OWASP's minimum.** No leak path was found in the inspected scope.

**Password storage**
- The API uses Identity's default `PasswordHasher` with no custom configuration (`services/api/Program.cs:24-36`).
- A hash from the local database, for the account created during manual check 3.6, decodes as Identity V3: PBKDF2 with HMAC-SHA512, 100,000 iterations, a 16-byte per-password random salt and a 32-byte subkey.
- Microsoft Learn documents IdentityV3 with 100,000 iterations as the default and says new apps "should use the PasswordHasher class" ([password-hasher options](https://learn.microsoft.com/aspnet/core/security/authentication/identity-configuration#password-hasher-options), [hash passwords](https://learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/password-hashing)).
- The hash is **salted, not peppered**. Microsoft Learn has no pepper guidance. See Password storage below.
- **Follow-up:** against the [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html) the work factor is **too low**.
  - OWASP's minimum for PBKDF2-HMAC-SHA512 is 220,000 iterations; the stored hashes use 100,000 (45 % of it).
  - OWASP prefers Argon2id and positions PBKDF2 for when FIPS-140 compliance is required.
  - See "Follow-up: OWASP Password Storage Cheat Sheet" below.

**Leak paths**
- Twelve leak paths were checked (Password leak surface below).
- None exposes passwords: nine are closed or not applicable; three are closed only in part:
  - Development-only error details
  - the data-protection key ring
  - local development (plain HTTP, and PostgreSQL published on every host interface)
- **Git history:** 47 commits across all refs, plus 35 dangling blobs, contain no real secret.

**Gaps against Microsoft Learn guidance.** None of these discloses passwords. The first three are the most material:

1. **Data-protection keys are unencrypted at rest** in the same PostgreSQL database as the users. Anyone who can read that database or its backups can forge an `oz_session` cookie for any user. Microsoft recommends an explicit key encryption mechanism for production.
2. **No rate limiting.** Per-account lockout is the only brake. Password spraying across accounts, lockout-based denial of service and unlimited registrations are all possible.
3. **The API's managed identity is the PostgreSQL Entra administrator.** That conflicts with Microsoft's least-privilege guidance for database access.
4. **Account enumeration:**
   - `register` returns 409 `email_taken`.
   - Login timing differs: a measured ~3 ms for an unknown email against ~46 ms for a known one.
   - Only a known email can reach `locked_out`.
5. **Logout does not revoke the session server-side.** Nothing rotates the security stamp, and revalidation runs at most every 30 minutes by default.
6. **CSRF relies on JSON-only bodies + SameSite=Lax + no CORS**, with no antiforgery token. The .NET 10 guidance calls cookie-authenticated APIs a CSRF risk and does not endorse this combination. A cross-site forced logout is plausible.
7. **Password policy:**
   - no banned- or breached-password check, which Microsoft calls the most important requirement;
   - a minimum of 10 characters, between Entra's 8 and Microsoft 365's recommended 14;
   - cookies are always persistent, without user consent.
8. **PBKDF2 work factor below OWASP** (found at 98a1cd5). The stored hashes used 100,000 iterations of PBKDF2-HMAC-SHA512 (45 % of OWASP's 220,000 minimum). OWASP prefers Argon2id. **Resolved in S-01:** the count is now 220,000, and older hashes upgrade at the next login (verified by test). The **pepper** moves to the password-reset scope (PRD US-03 acceptance criteria; roadmap `account-recovery-email`).

## Detailed Findings

### Password storage (hashing, salt, pepper)

- **Hasher:** the default `PasswordHasher<AppUser>`. `AddIdentityCore<AppUser>` configures no hasher, compatibility mode or iteration count (`services/api/Program.cs:24-36`).
- **Empirical check:** in the local database, the `password_hash` of `owner-1790674693@example.test` is 61 bytes decoded, with format marker `0x01` (V3), PRF `HMACSHA512`, 100,000 iterations, a 16-byte salt and a 32-byte subkey. This was a read-only decode in this session.
- **Microsoft Learn:**
  - CompatibilityMode "Defaults to IdentityV3".
  - IterationCount "defaults to 100000" ([identity-configuration](https://learn.microsoft.com/aspnet/core/security/authentication/identity-configuration#password-hasher-options); [PasswordHasherOptions.IterationCount](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.passwordhasheroptions.iterationcount)).
  - Learn does not state the PRF, salt or subkey sizes; they come from the empirical decode above.
- **Storage:** the `users` table has one password-derived column, `password_hash text` (`services/api/Data/Migrations/20260929071539_AccountsAndAnimals.cs:66`).
  - The plaintext password exists only in the request DTO (`services/api/Auth/CredentialsRequest.cs:3`).
  - It is passed only to `UserManager.CreateAsync` and `SignInManager.PasswordSignInAsync` (`services/api/Auth/AuthEndpoints.cs:27`, `:42-46`).
  - `AppUser` adds no properties (`services/api/Auth/AppUser.cs`).
- **Salt, not pepper:**
  - The salt is random per password and stored inside the hash, as the V3 format does.
  - No secret outside the database goes into the hash, and Identity has no pepper option.
  - Microsoft Learn has no pepper guidance (search "password hashing pepper … IPasswordHasher", 2026-09-29). Its closest statement steers new apps to `PasswordHasher` rather than custom PBKDF2 (`KeyDerivation.Pbkdf2` "shouldn't be used in new apps") ([hash passwords](https://learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/password-hashing)).
  - A pepper would need a custom `IPasswordHasher<AppUser>` wrapper, a key in Key Vault, and a versioning scheme for rotation. It protects only against a database-only leak, such as a dump or a backup.
- **Tests:** none assert the hash format or algorithm.

### Password policy

- Minimum length is 10 (`Program.cs:27`); the framework default is 6.
- Digit, uppercase, lowercase and non-alphanumeric requirements are off (`Program.cs:28-31`).
- `RequiredUniqueChars` keeps its default of 1, so `aaaaaaaaaa` is accepted. There is no maximum length and no banned- or breached-password check.
- `AuthTests.cs:37-47` proves that 9 characters gives 400 `password_too_short` and 10 gives 201.
- **Microsoft guidance:**
  - "almost every rule you impose on your users results in a weakening of password quality".
  - "we recommend a minimum of 14 characters".
  - "The most important password requirement… is to ban the use of easy-to-guess passwords" ([Microsoft 365 password policy recommendations](https://learn.microsoft.com/microsoft-365/admin/misc/password-policy-recommendations)).
  - Entra's principles: minimum length 8, no composition rules, block common passwords ([Entra password FAQ](https://learn.microsoft.com/entra/identity/authentication/tutorial-password-policy-overview-frequently-asked-questions#faq)).
- **Comparison:** length over composition matches that direction. The missing banned-password check is the gap; Identity would need a custom `IPasswordValidator` (inference).

### Lockout

- The settings are 5 failures and a 5-minute lockout (`Program.cs:32-33`), both equal to the framework defaults. `AllowedForNewUsers` keeps its default of true.
- Login passes `lockoutOnFailure: true` (`AuthEndpoints.cs:46`).
- `AuthTests.cs:99-119` proves:
  - failures 1–4 return `invalid_credentials`;
  - failures 5 and 6 return `locked_out`;
  - the correct password is refused while the account is locked.
- **Risk:** anyone who knows an email can keep that account locked with 5 bad attempts every 5 minutes. No per-IP or global throttle exists; see Rate limiting.
- Microsoft's legacy ASP.NET Identity page warns that lockout "makes your login susceptible to DOS lockouts. We recommend you use account lockout only with 2FA" ([ASP.NET Identity account confirmation](https://learn.microsoft.com/aspnet/identity/overview/features-api/account-confirmation-and-password-recovery-with-aspnet-identity#examine-the-code)).
- For comparison, Entra smart lockout is 10 failures, then 1 minute, with repeated identical bad passwords ignored ([Entra password policies](https://learn.microsoft.com/entra/identity/authentication/concept-sspr-policy#microsoft-entra-password-policies)).

### Account enumeration

- **Register:** 409 `email_taken` for an existing email in any letter case (`AuthEndpoints.cs:68-71`; `AuthTests.cs:22-35`).
- **Login:** 401 `invalid_credentials` for both an unknown email and a wrong password (`AuthEndpoints.cs:53-54`; `AuthTests.cs:89-97`). This matches the anti-enumeration principle on Microsoft's legacy password-recovery page.
- **Timing, measured locally in this session** (Development, one API process, three wrong-password logins against a fresh account):
  - An unknown email returned 401 in 27, 10, 3 and 3 ms; the first two are warm-up.
  - A known email with a wrong password returned 401 in 61, 46 and 46 ms.
  - The ~43 ms difference is consistent with PBKDF2 running only for existing users, so response time reveals existence.
- **Lockout state:** only a known email can reach `locked_out` (inference from `AuthEndpoints.cs:53-54`; untested).
- Learn has no guidance for register responses. Register enumeration is a residual risk, not a documented violation.

### Cookie session

- **Scheme:** the Identity application scheme via `AddIdentityCookies` (`Program.cs:23`), configured in `Program.cs:37-58`.
- **Attributes:**
  - name `oz_session` (`:39`)
  - HttpOnly (`:40`)
  - SameSite=Lax (`:41`)
  - `Secure`: `SameAsRequest` in Development, `Always` elsewhere (`:42-44`). App Service sets `ASPNETCORE_ENVIRONMENT=Production` (`infra/modules/app-service.bicep:48-51`).
  - 14 days, sliding (`:45-46`)
  - redirects replaced by 401/403 (`:48-57`)
- **Proof:**
  - The local `.http` run in this session showed `Set-Cookie: oz_session=…; expires=+14 days; path=/; samesite=lax; httponly`.
  - `AuthTests.cs:135-146` proves 401 with no `Location` header.
  - No test covers `Secure` in Production, because tests run in Development (`services/api.Tests/ApiFactory.cs:15-24`).
- **Persistent cookies:** register and login both use `isPersistent: true` (`AuthEndpoints.cs:33`, `:45`).
  - Microsoft: "This persistence should only be enabled with explicit user consent with a 'Remember Me' checkbox" ([cookie auth](https://learn.microsoft.com/aspnet/core/security/authentication/cookie#persistent-cookies)).
  - The plan excluded a remember-me checkbox (`context/changes/account-and-first-animal/plan.md:33`).
- **Microsoft defaults:** HttpOnly true, SameSite Lax, and `SecurePolicy` default `SameAsRequest`. The Production `Always` setting is therefore stricter than the default ([CookieAuthenticationOptions.Cookie](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.authentication.cookies.cookieauthenticationoptions.cookie)).

### Logout and session revocation

- Logout calls `SignInManager.SignOutAsync()` (`AuthEndpoints.cs:57-61`).
  - The local run showed expiring `Set-Cookie` headers for `oz_session`, `Identity.External` and `Identity.TwoFactorUserId`.
  - `AuthTests.cs:121-133` proves the same client then gets 401.
- **No server-side revocation:**
  - The cookie ticket is stateless and nothing rotates `security_stamp`; the column is in `…AccountsAndAnimals.cs:67`, and no code calls `UpdateSecurityStampAsync`.
  - A copied `oz_session` value therefore stays valid after logout until it expires.
  - Microsoft: "Call userManager.UpdateSecurityStampAsync(user) to force existing cookies to be invalided the next time they are checked" ([identity-configuration](https://learn.microsoft.com/aspnet/core/security/authentication/identity-configuration)).
  - `SecurityStampValidatorOptions.ValidationInterval` "Defaults to 30 minutes" ([API doc](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.securitystampvalidatoroptions.validationinterval)).
- **Unconfirmed:** whether `AddIdentityCore` + `AddSignInManager` registers `ISecurityStampValidator` in .NET 10, as full `AddIdentity` does. See Open Questions.

### CSRF

- There is no `AddAntiforgery`/`UseAntiforgery` and no CORS configuration (grep over `services/api`, 2026-09-29). Production calls stay same-origin through the SWA linked backend (`infra/modules/static-web-app.bicep:24-31`).
- **Authenticated JSON endpoint:** a form-encoded `POST /api/animals` returns 415 (`AnimalTests.cs:62-74`).
- **Anonymous auth routes:** a non-JSON body returns 401, not 415. This is known and untested (`context/changes/account-and-first-animal/notes/2026-09-29-pickup.md:54`). Cross-site form login or register is therefore rejected.
- **Logout** has no body parameter (`AuthEndpoints.cs:57-61`), so a cross-site POST can trigger it. SameSite=Lax withholds the session cookie, but the response still carries the deleting `Set-Cookie`. Whether browsers apply it cross-site is unconfirmed. The impact is forced sign-out only.
- **Microsoft (.NET 10):**
  - "If cookies are used to store authentication tokens and to authenticate API requests on the server, CSRF is a potential problem."
  - "Do not disable antiforgery validation for browser-accessible endpoints that rely on cookies for authentication" ([anti-request-forgery](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery)).
  - SameSite offers "some protection" ([SameSite](https://learn.microsoft.com/aspnet/core/security/samesite)).
- The .NET 11 section of the same page says JSON-bound endpoints are not the form-post vector. No .NET 10 page endorses JSON-only + Lax as sufficient.

### Authorization and owner isolation

- **Fallback policy:** every endpoint requires an authenticated user unless it opts out (`Program.cs:60-62`).
- **Anonymous endpoints** (exact set in this build):
  - `GET /openapi/v1.json`, Development only (`Program.cs:71-75`; `OpenApiTests.cs`)
  - `GET /api/health` (`Program.cs:82-92`)
  - `POST /api/auth/register`, `/login` and `/logout` (`AuthEndpoints.cs:12-16`)
- **Roles and policies:** none are used. The Identity roles and claims tables exist but no code reads them (`AppDbContext.cs`).
- **Owner isolation:**
  - The user id comes from the cookie's NameIdentifier claim through `UserManager.GetUserId` (`AnimalEndpoints.cs:55-56`).
  - Every animal query goes through `OwnedAnimals.ForUser`, which starts from `animal_members WHERE user_id = @id` (`services/api/Animals/OwnedAnimals.cs:44-48`).
  - `IsolationTests.cs:8-45` proves each account lists only its own animals, B gets 404 for A's animal, and `hasAnimals` ignores other accounts. This session's break-check (removing the filter) turned both isolation tests red.
- **Residual:** `AppDbContext` still exposes the `Animals`/`AnimalMembers` DbSets publicly (`AppDbContext.cs:14-17`). Only convention and review (check 3.7) keep endpoints off them.

### Data protection (cookie keys)

- **Configuration:** `AddDataProtection().PersistKeysToDbContext<AppDbContext>()` with no `ProtectKeysWith*` and no `SetApplicationName` (`Program.cs:19-20`).
- **Empirical check:** the local `data_protection_keys.xml` row contains a `<masterKey>` element under the framework comment `Warning: the key below is in an unencrypted form.` (read-only inspection in this session).
- **Microsoft:**
  - "If you specify an explicit key persistence location, the data protection system deregisters the default key encryption at rest mechanism, so keys are no longer encrypted at rest. It's recommended that you additionally specify an explicit key encryption mechanism for production deployments" ([key storage providers](https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-storage-providers)).
  - The options are `ProtectKeysWithAzureKeyVault` (needs Get, Wrap Key and Unwrap Key) or `ProtectKeysWithCertificate`. Apps that run in multiple environments "must set the default application discriminator" ([configuration overview](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)).
- **Consequence:**
  - Read access to the database or its backups (7-day retention, `infra/modules/postgres.bicep:31-34`) gives the key ring and the user ids. That is enough to forge a valid `oz_session` for any user.
  - Passwords are not disclosed this way: the cookie holds claims, not the password.
  - Production database readers are limited to the API managed identity, the only Entra admin (`infra/modules/postgres.bicep:49-57`), behind a firewall that admits only the App Service outbound IPs (`infra/modules/postgres-firewall.bicep:15-23`).
- **Constraint for a fix:** the CI deploy identity cannot create role assignments (`context/changes/account-and-first-animal/plan.md:25`). A Key Vault key for data protection therefore needs a bootstrap or owner step for its RBAC.

### Password leak surface

| # | Path | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Database columns | closed | Only `password_hash` is derived from the password (`…AccountsAndAnimals.cs:60-74`); `user_tokens` is unused because no 2FA/reset routes exist (`AuthEndpoints.cs:6-7`) |
| 2 | Logging | closed; relies on framework defaults | Log levels `Information`/`Warning` (`appsettings.json:2-7`). A grep over `services/api` finds none of `EnableSensitiveDataLogging`, `EnableDetailedErrors`, `AddHttpLogging`, W3C logging, `ILogger` calls. Microsoft calls sensitive-data logging "a security risk, as it may expose passwords" ([EF Core in Blazor](https://learn.microsoft.com/aspnet/core/blazor/blazor-ef-core#enable-sensitive-data-logging)); parameter masking is the EF default |
| 3 | Responses | closed in Production; partially closed in Development | Errors carry only status + `code` (`ApiProblem.cs:8-9`). No developer exception page is configured. The automatic Development exception page can show request headers/cookies (framework default, local only) |
| 4 | Telemetry | not applicable | No Application Insights/OpenTelemetry packages or settings (`ogarniamy-zwierzaki-api.csproj`, `Directory.Packages.props`, `app-service.bicep:47-61`) |
| 5 | Config and infra | closed | No connection string in `appsettings.json`. The Azure connection string has no password (`app-service.bicep:57-59`). `passwordAuth: 'Disabled'` (`postgres.bicep:41-45`). `.env` is gitignored (`.gitignore:5`); user-secrets live outside the repo |
| 6 | Git history | closed | 47 commits over all refs plus 35 dangling blobs scanned for password, key, token and private-key patterns: placeholder or doc lines only; no `.env` ever added |
| 7 | Tests and samples | closed | Synthetic passwords (`TestAccounts.cs:11`, `ogarniamy-zwierzaki-api.http:12-25`); Testcontainers throwaway credentials |
| 8 | CI/CD | closed | OIDC with `vars.*` only; no `secrets.*`; SWA token masked (`deploy.yml:99-117`); smoke test sends no credentials (`scripts/smoke.sh`) |
| 9 | Transport | closed in Production; local dev is plain HTTP | `httpsOnly: true`, TLS 1.2 (`app-service.bicep:41,45`); cookie `Secure` outside Development; no `UseHsts` (see Open Questions); local `http://localhost:5180` |
| 10 | Web client | not applicable yet | `apps/web` has no password form until Phase 4 |
| 11 | Data-protection key ring | partially open (sessions, not passwords) | See Data protection |
| 12 | Backups | covered by the same controls | Hashes and keys are in backups with the server's access controls (`postgres.bicep:31-34`) |

Local-development caveat: `compose.yaml:10` publishes `${POSTGRES_PORT:-5432}:5432` without `127.0.0.1:`, so the local database listens on all host interfaces. It is protected by the `.env` password and the host firewall.

### Rate limiting and transport extras

- **Rate limiting:** no `AddRateLimiter`/`UseRateLimiter` (grep over `services/api`).
  - Password spraying across accounts is limited only by per-account lockout.
  - Unknown-email attempts cost a lookup.
  - `register` is unlimited, and each call costs one PBKDF2 hash.
  - Microsoft lists "Preventing Abuse" and "Enhancing Security" as rate-limiting purposes and warns that partitioning by client IP is spoofable ([rate limiting](https://learn.microsoft.com/aspnet/core/performance/rate-limit)). No Learn page specifically mandates it for authentication endpoints.
- **HSTS:** `UseHttpsRedirection` is present (`Program.cs:77`); `UseHsts` is absent. Microsoft: "The default API projects don't include HSTS because it's generally a browser only instruction" ([enforcing SSL](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl)). The browser talks to the SWA domain, so HSTS belongs at the SWA edge; not verified.

### Database access (Azure)

- Entra-only authentication with a managed identity matches "Use Entra instead of database local authentication" and "Use managed identities for application access" ([PostgreSQL security overview](https://learn.microsoft.com/azure/postgresql/security/security-overview)).
- **Gap:** the API identity is itself the Entra administrator (`infra/modules/postgres.bicep:49-57`).
  - The Entra admin "Gets the same privileges as the original PostgreSQL administrator", and "Using a group account as an administrator enhances manageability" ([Entra concepts](https://learn.microsoft.com/azure/postgresql/security/security-entra-concepts)).
  - The least-privilege pattern would be: a group as admin, and the API as a non-admin role with only the grants it needs (inference).
  - Trade-off: the plan relies on the API owning the schema it migrates (`context/changes/account-and-first-animal/plan.md:55`).

### Follow-up: OWASP Password Storage Cheat Sheet

Source: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html, fetched 2026-09-29. Quotes are verbatim from the fetched page.

| OWASP guidance | Our implementation | Verdict |
| --- | --- | --- |
| **Algorithm order:** Argon2id first ("m=19456 (19 MiB), t=2, p=1"), then scrypt, then bcrypt for legacy systems. PBKDF2 is listed for when FIPS-140 compliance is required. | PBKDF2 (Identity default); no FIPS-140 requirement exists in the PRD | **Weaker than preferred.** Acceptable, but not OWASP's first choice. Argon2id has no built-in .NET implementation and would need a third-party library behind a custom `IPasswordHasher` (inference). |
| **PBKDF2 work factor:** "PBKDF2-HMAC-SHA256: 600,000 iterations (recommended)"; "PBKDF2-HMAC-SHA512: 220,000 iterations" | HMAC-SHA512, **100,000** iterations (decoded hash, see Password storage) | **Gap:** 100,000 is 45 % of OWASP's SHA-512 minimum, and Microsoft's default is below OWASP's floor. |
| **Work factor tuning:** "calculating a hash should take less than one second"; tune on your own servers | A known-email failed login took ~46 ms locally (Enumeration timing above) | 220,000 iterations would cost roughly 2.2× (inference: PBKDF2 cost scales linearly with iterations), well under one second. On App Service F1 each hash counts against the 60 CPU-minute daily quota (`plan.md:465`). |
| **Upgrading the work factor:** "wait until the user next authenticates, then re-hash their password with the new work factor" | Identity stores the iteration count inside each V3 hash (decoded above). **Verified 2026-09-29:** with `IterationCount = 220000`, a stored 100,000-iteration hash is re-hashed to 220,000 at the next successful sign-in (`services/api.Tests/PasswordHashingTests.cs`, `A_weaker_stored_hash_is_rehashed_at_the_next_sign_in`). | **Adopted in S-01:** `PasswordHasher:IterationCount = 220000` (`services/api/appsettings.json`, bound in `Program.cs`). Existing accounts upgrade at their next login. |
| **Salt:** "a unique, randomly generated string that is added to each password"; libraries "generate and manage salts internally" | 16-byte random salt per password, generated by Identity and stored in the hash | **Meets.** |
| **Pepper:** "Consider using a pepper". It is optional. "Peppers are secrets and should be stored in 'secrets vaults' or HSMs", "should not be stored along with the generated hash". It is applied before hashing or as a post-hash HMAC. "changing a pepper will require forcing all users whose passwords were protected by the previous pepper to reset their passwords" | None | **Optional, not a violation.** A pepper protects against database-only leaks, at the cost of a Key Vault secret and a forced password reset if it is ever rotated or compromised. With no password reset flow until FR-016, a pepper compromise now would lock users out. |
| **Password length:** the 72-byte limit applies to bcrypt only | PBKDF2 has no such limit; Identity sets no maximum | **Not applicable.** Consider a sane maximum such as 128–256 characters against CPU abuse (inference; OWASP's cheat sheet does not state this). |
| **Unicode:** the library "should be compatible with all Unicode codepoints", including NULL bytes | Identity hashes the UTF-8 bytes of the .NET string (framework behaviour) | **Meets** (not tested). |
| **Algorithm transparency:** "You do not need to hide which password hashing algorithm is used" | V3 format marker and parameters are stored with the hash | **Meets.** The self-describing format also eases future upgrades, in line with OWASP's PHC-format advice. |

**Net effect on the verdict**
- Passwords are salted and hashed with a slow key-derivation function and do not leak through any inspected path.
- The **work factor is below OWASP's minimum** for the algorithm in use. Microsoft Learn documents the lower value as the default without recommending a higher one.
- The cheapest improvement is `PasswordHasherOptions.IterationCount = 220_000` or more, with rehash on next login. Argon2id and a pepper are larger decisions.

## Code References

- `services/api/Program.cs:19-20` - Data protection persisted to PostgreSQL, no key encryption
- `services/api/Program.cs:23-36` - Identity core, password and lockout options
- `services/api/Program.cs:37-58` - `oz_session` cookie options and 401/403 events
- `services/api/Program.cs:60-62` - fallback authorization policy
- `services/api/Auth/AuthEndpoints.cs:12-16` - anonymous auth group (register, login, logout)
- `services/api/Auth/AuthEndpoints.cs:22-85` - register/login/logout handlers and error-code mapping
- `services/api/Animals/OwnedAnimals.cs:44-48` - owner-scoped query root
- `services/api/Data/Migrations/20260929071539_AccountsAndAnimals.cs:28-40,60-74` - `data_protection_keys` and `users` columns
- `infra/modules/postgres.bicep:41-57` - Entra-only auth; API identity as administrator
- `infra/modules/app-service.bicep:41-61` - HTTPS-only, TLS 1.2, Production environment, passwordless connection string
- `compose.yaml:10` - local PostgreSQL port binding

## Architecture Insights

- Identity uses Microsoft defaults wherever the code is silent: hasher, lockout, SameSite and HttpOnly. The deliberate departures are:
  - length-only password policy
  - `Secure=Always` outside Development
  - status codes instead of redirects
  - persistent 14-day cookies
  - hand-written endpoints instead of `MapIdentityApi`
- One component, `OwnedAnimals`, scopes every animal query to the user, and tests with a break-check back it. This is the isolation pattern later slices extend.
- The one secret that protects sessions (the key ring) shares a store and an access principal with the data it protects. That is the main structural weakness, and it concerns sessions, not passwords.

## Historical Context (from prior changes)

- `context/changes/account-and-first-animal/plan.md:33` - Password reset, email verification, remember-me, MFA and rate controls beyond lockout were deferred. Reset/verification are PRD FR-015/FR-016 and MFA is FR-017 (`context/foundation/prd.md:302-304`). Status: still accurate.
- `context/changes/account-and-first-animal/plan.md:56` - The CSRF defence is JSON-only + SameSite=Lax + no CORS. Status: implemented as planned. Microsoft .NET 10 guidance does not endorse it as sufficient (CSRF above).
- `context/changes/account-and-first-animal/plan.md:57` - Keys were persisted to PostgreSQL so sessions survive restarts. Status: works; the plan did not mention encryption at rest, which Microsoft recommends.
- `context/changes/account-and-first-animal/plan.md:232-240` - The Identity, cookie and lockout contract. Status: matches the code on every listed value.

## Related Research

Not applicable: there is no other `research.md` under `context/changes/**` or `context/archive/**` on authentication.

## Open Questions

1. **Security stamp validation:** does `AddIdentityCore<AppUser>().AddSignInManager()` register `ISecurityStampValidator` in .NET 10? If not, even a future `UpdateSecurityStampAsync` would not revoke cookies. Check it with a test that rotates the stamp and uses a short `ValidationInterval`.
2. **SWA edge headers:** does Azure Static Web Apps send HSTS for the default `*.azurestaticapps.net` host? This can be checked in Phase 5 with `curl -I`.
3. **Cross-site logout:** do browsers apply the deleting `Set-Cookie` from a cross-site logout POST?
4. **Decisions for the owner** (not for research):
   - encrypt the key ring (Key Vault key via a bootstrap RBAC step) and set an application name;
   - add rate limiting to the auth routes;
   - split the PostgreSQL admin from the API identity;
   - make register non-enumerating and equalise login timing;
   - add a banned-password validator;
   - ~~raise `PasswordHasherOptions.IterationCount` to 220,000~~ decided and done in S-01;
   - ~~pepper~~ decided: part of the password-reset work (PRD US-03, roadmap `account-recovery-email`);
   - optionally move to Argon2id (see the OWASP follow-up);
   - revoke sessions on logout (rotate the security stamp);
   - add antiforgery or an Origin check.
