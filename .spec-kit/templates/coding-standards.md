# Coding Standards

**Updated:** YYYY-MM-DD · **Budget:** 120 lines target / 180 hard ceiling

The rules `/implement` follows and `/validate` checks against. Only include rules that are
**decidable by reading code** — vague aspirations ("write clean code") are unenforceable and
waste budget. Prefer deleting a rule the linter already enforces.

Rules established by a specific spec cite it: `(est. 0001)`.

---

## Naming

| Element | Convention | Example |
|---|---|---|
| Files | | |
| Classes / types | | |
| Interfaces | | |
| Functions / methods | | |
| Constants | | |
| Database tables | | |
| Database columns | | |
| API routes | | |
| JSON fields | | |
| Test methods | | |

## Layering

The dependency rules. `/validate` treats a violation as a High-severity finding.

1. <e.g. `api/*` may not reference the persistence layer directly>
2. ...

Mirrors the Layering Rules section of `architecture.md` — keep the two consistent.

## Error Handling

- **Envelope:** <e.g. RFC 7807 ProblemDetails for all HTTP errors>
- **Expected failures:** <e.g. returned as `Result<T>`, never thrown>
- **Unexpected failures:** <e.g. bubble to global handler; never swallowed>
- **Never:** catch-and-ignore, `catch (Exception) { }`, returning 200 on failure
- **Error codes:** <format, e.g. `<entity>.<operation>.<condition>`>

## Logging

- **Library / pattern:** <structured logging, no string interpolation into the message>
- **Correlation:** <every request carries a trace id, propagated to logs and error responses>
- **Levels:** Debug <when> · Information <when> · Warning <when> · Error <when>
- **Never log:** passwords, tokens, full PII, request bodies containing candidate data

## Validation

- **Where:** <e.g. request DTOs at the API boundary; invariants inside the domain>
- **Library:** <name>
- **Rule:** the domain never trusts the API layer to have validated

## Security

- No secrets in source or in committed config. Configuration keys only.
- All endpoints authorized by default; anonymous access is explicit and justified.
- Parameterised queries only — no string-concatenated SQL.
- PII in responses is limited to what the caller's role needs.

## Testing

- **Framework:** <name>
- **Naming:** <e.g. `Method_Scenario_ExpectedResult`>
- **Structure:** Arrange / Act / Assert, one logical assertion per test
- **Required coverage:** every `AC-n` has at least one test; every bug fix adds a regression test
- **No:** tests that assert nothing, tests depending on execution order, `Thread.Sleep`
- **Test data:** builders/factories, not shared mutable fixtures

## Frontend Specific

- Every async surface implements all four states: loading, empty, error, success.
- No business logic in components — it belongs in hooks or services.
- Accessibility: semantic elements, labelled controls, keyboard-operable interactions.
- No hard-coded strings destined for the UI where the project has i18n.

## Documentation

- Public APIs carry doc comments explaining *why*, not restating the signature.
- Comments explain intent and non-obvious constraints; no commented-out code.
- Match the density and idiom of surrounding code.

## Git

- Branch naming: <convention>
- Commit messages: <convention>
- No commits directly to <default branch>

## Project-Specific Rules

Appended by `/implement` when a spec establishes a project-wide convention. One line each,
citing the originating spec.

- <rule> (est. NNNN)
