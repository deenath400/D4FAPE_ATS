# Coding Standards

**Updated:** 2026-08-05 · **Budget:** 120 lines target / 180 hard ceiling

The rules `/implement` follows and `/validate` checks against. Only include rules that are
**decidable by reading code**.

Greenfield: these are the mainstream conventions of the chosen ecosystems — C#/.NET on the
backend, TypeScript/React on the frontend — not personal preference. No linter is configured
yet, so nothing here is delegated to one. When `dotnet format`/`.editorconfig` and ESLint land,
delete the rules they enforce and name the linter instead.

Rules established by a specific spec cite it: `(est. 0001)`.

---

## Naming

| Element | Convention | Example |
|---|---|---|
| Files (C#) | PascalCase, one public type per file, filename matches type | `ApplicationService.cs` |
| Files (TS/React) | kebab-case modules; PascalCase component files | `application-list.ts`, `ApplicationCard.tsx` |
| Classes / types | PascalCase | `Requisition`, `ApplicationDto` |
| Interfaces | `I` + PascalCase (C#); no prefix (TS) | `IApplicationService`, `ApplicationFilters` |
| Functions / methods | PascalCase (C#), camelCase (TS) | `AdvanceStage`, `advanceStage` |
| Private fields (C#) | `_camelCase` | `_repository` |
| Constants | PascalCase (C#), UPPER_SNAKE_CASE (TS module constants) | `MaxUploadBytes`, `MAX_UPLOAD_BYTES` |
| Database tables | snake_case, plural | `applications` |
| Database columns | snake_case | `submitted_at` |
| API routes | lowercase kebab-case, plural nouns, no verbs | `/api/requisitions/{id}/applications` |
| JSON fields | camelCase | `submittedAt` |
| Test methods | `Method_Scenario_ExpectedResult` (C#); `describes what it does` (TS) | `AdvanceStage_WhenRejected_Throws` |

Indentation: 4 spaces in C#, 2 spaces in TS/TSX. UTF-8, LF line endings.

## Layering

The dependency rules. `/validate` treats a violation as a High-severity finding.

1. `ui/*` reaches the backend only over HTTP — no EF Core reference, connection string, or
   SQLite file access from the Next.js app.
2. `api/*` depends on `service/*`, never on `db/*`. No `DbContext` or EF Core type in a
   controller, endpoint, or request DTO.
3. `service/*` owns transactions and business rules, and is the only layer that calls `db/*`.
4. `db/*` exposes entities and query results only. HTTP types, DTOs, and `ClaimsPrincipal`
   never cross into it.
5. `shared/*` depends on nothing in `api/*`, `service/*`, or `db/*`.

Mirrors the Layering Rules section of `architecture.md` — keep the two consistent.

## Error Handling

- **Envelope:** RFC 7807 ProblemDetails for every HTTP error response, including 401 and 403.
- **Expected failures:** modelled as return values from `service/*` (not-found, conflict,
  validation), translated to status codes at `api/*`. Do not throw for expected outcomes.
- **Unexpected failures:** bubble to the global exception handler. Never swallowed.
- **Never:** `catch (Exception) { }`, catch-and-ignore, or returning 200 on failure.
- **Error codes:** `<entity>.<operation>.<condition>`, e.g. `application.advance.invalid-stage`.

## Logging

- **Library / pattern:** `ILogger<T>` with structured message templates. Never interpolate
  values into the message string — pass them as named parameters.
- **Correlation:** every request carries a trace id, propagated into logs and into the
  `traceId` of ProblemDetails responses.
- **Levels:** Debug — local diagnostics · Information — state changes worth auditing ·
  Warning — recovered or rejected input · Error — unhandled failure.
- **Never log:** passwords, tokens, JWTs, CV file contents, or candidate PII (name, email,
  phone). Log entity ids instead.

## Validation

- **Where:** request DTOs at the `api/*` boundary; invariants re-checked inside `service/*`.
- **Library:** TBD — the first API spec chooses it.
- **Rule:** the domain never trusts the API layer to have validated.
- **Uploads:** every uploaded file is checked for content type and size limit before it is
  written anywhere. Never trust the client-supplied filename or extension.

## Security

- No secrets in source or committed config. Configuration keys only.
- All endpoints authorized by default. Anonymous access is explicit, justified in the spec,
  and limited to the public portal surface (registration, job search, apply).
- A `Candidate` principal may only ever read or write its own applications — ownership is
  checked server-side on every candidate-facing endpoint, never inferred from the request.
- Staff-only endpoints must be unsatisfiable by a candidate token.
- Parameterised queries only. No string-concatenated SQL; no raw SQL without a written reason.
- Uploaded files are never served from a path the client controls.

## Testing

- **Framework:** xUnit (backend), Vitest + Testing Library (frontend).
- **Naming:** as in the Naming table.
- **Structure:** Arrange / Act / Assert, one logical assertion per test.
- **Required coverage:** every `AC-n` has at least one test; every bug fix adds a regression
  test.
- **No:** tests that assert nothing, tests depending on execution order, `Thread.Sleep`.
- **Test data:** builders/factories, not shared mutable fixtures.
- **Database tests:** each test gets its own SQLite database file or connection — never a
  shared file, given the single-writer constraint.

## Frontend Specific

- Every async surface implements all four states: loading, empty, error, success.
- No business logic in components — it belongs in hooks or service modules.
- Server Components by default; `"use client"` only where interactivity requires it.
- Accessibility: semantic elements, labelled controls, keyboard-operable interactions.
- Never place a token, secret, or backend-only value in a `NEXT_PUBLIC_*` variable.

## Documentation

- Public APIs carry doc comments explaining *why*, not restating the signature.
- Comments explain intent and non-obvious constraints; no commented-out code.
- Match the density and idiom of surrounding code.

## Git

- Branch naming: `NNNN-slug` matching the spec id, e.g. `0001-candidate-registration`.
- Commit messages: imperative subject under 72 chars, prefixed with the spec id — `0001: add
  candidate registration endpoint`.
- No commits directly to `main`.

## Project-Specific Rules

Appended by `/implement` when a spec establishes a project-wide convention. One line each,
citing the originating spec.

- <none yet>

## Related Specs

None — this is the first artifact in the repository.
