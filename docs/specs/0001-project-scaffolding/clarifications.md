# Clarifications — 0001 Project Scaffolding and Walking Skeleton

A verbatim log of ambiguities raised during `/specify` and how they were resolved. This is an
audit trail: when someone later asks "why does it work this way", the answer is here.

Do not paraphrase the user's answer into what you wish they had said. Record what they chose.

---

## Round 1 — 2026-08-05

### C-1 — How much must the skeleton prove end to end?

**Ambiguity.** "Stand up the API, app, EF Core, test projects and .gitignore" reads equally as
three unrelated chores (each side builds in isolation) or as one feature (a thin request path
proven from browser to database). The readings differ materially: the isolated reading leaves
the HTTP seam between the two deployables — base URL configuration, cross-origin posture,
serialisation — unproven until some later feature spec pays to discover it, and leaves the five
Layering Rules unexercised.

**Options presented.**
1. Walking skeleton — a frontend page fetches from an API endpoint traversing `api → service →
   db`, proving all five Layering Rules and the HTTP seam on day one; costs the most and adds a
   small `system` component area.
2. Two independent halves — each side builds, tests and runs alone with no call between them;
   leaves the seam unproven until the first feature spec pays for it.
3. Build-only — projects compile and tests run, nothing serves a request; cheapest, and leaves
   both the seam and the layering enforcement untested.

**Answer.** Option 1 — "Walking skeleton (Recommended). One thin path, `api → service → db`,
proving the five Layering Rules and the HTTP seam."

**Impact.** Established G-2 and G-3. Produced FR-5, FR-6, FR-7 and AC-7, AC-10, AC-12. Is the
reason `service/system` exists at all: a status check could otherwise read the database from the
API layer, which would prove nothing about Layering Rule 3.

---

### C-2 — Is authentication wiring part of scaffolding?

**Ambiguity.** "Project scaffolding" is silent on whether the authentication substrate counts as
scaffolding. Including it roughly doubles the spec and forces a decision `architecture.md`
explicitly left open — where the Next.js app stores the token, and how server components and
route handlers read it. Excluding it risks the frontend being built against a call pattern that
authentication later invalidates.

**Options presented.**
1. Out — scaffold ships with no auth; the next spec owns Identity, roles, JWT issuance and token
   storage as one coherent piece.
2. Identity tables only — the initial migration creates the Identity schema but no endpoints or
   policies exist; a half-decision that still pre-empts the token question.
3. Full auth — Identity, JWT issuance, role policies and a login flow in this spec; largest
   scope, and forces the unresolved token-storage decision under scaffolding pressure.

**Answer.** The user first overrode the recommendation and specified the architecture they want,
verbatim:

> implement identity endpoints in be and for FE use next auth to manage login
>
> fe should always invoke be through a handler (sort of a procy which appends jwt token by
> decoding next auth session which store access and refresh tokens)

The packaging choice was then put to them separately, with the estimate that full auth roughly
doubles this spec. They chose to split:

> **Split: 0001 scaffold, 0002 auth** — "0001 establishes the proxy-handler pattern on an
> unauthenticated endpoint; 0002 fills it with Identity, NextAuth and refresh-token handling."

**Impact.** Two distinct outcomes, recorded separately below because they resolve differently.

**Impact (a) — deferred to `0002`.** ASP.NET Core Identity, JWT issuance and validation,
NextAuth, role policies, login and logout, and refresh-token rotation are all Non-Goals of this
spec. The `Jwt__Issuer`, `Jwt__Audience` and `Jwt__SigningKey` keys presently marked required in
`tech-stack.md` are not required to run anything this spec ships; they become required in `0002`.
The deferral is cheap under SQLite specifically because adding the Identity schema later is a
table *addition*, which is the one class of schema change SQLite performs without rebuilding
existing tables.

**Impact (b) — decided and in scope now.** The proxy pattern itself is structural, so it is
established here rather than deferred. Produced FR-8 and AC-12, AC-13, AC-14. Spec `0002` adds
token attachment to one existing chokepoint instead of rewriting every call site written in the
interim.

---

### C-2a — Where the Next.js application stores the token

**Recorded as decided, implemented by `0002`. Not to be re-opened.**

`architecture.md` currently states: "Token handling across the HTTP boundary is unresolved. The
API issues the JWT; where the Next.js app stores it and how server components and route handlers
read it is TBD, settled by the first authentication spec." The user's answer to C-2 settles it,
ahead of that spec:

- The backend issues the JWT. Identity endpoints live in the backend.
- NextAuth manages login on the frontend and holds **both the access token and the refresh
  token** in the session, server-side.
- All browser-originated traffic reaches the backend through a server-side proxy handler, so the
  browser never holds, sees or sends the backend JWT, and never contacts the API origin directly.
- The JWT is attached by a shared server-side invoke function, not by the proxy handler — see
  C-7, which refines this point after the user answered Q-1. The function decodes the NextAuth
  session and attaches the token to every outbound backend call.

This spec implements the structure — the proxy chokepoint and the shared invoke function, both
carrying no token. `0002` implements the rest. The decision is recorded here so `0002` inherits
it rather than re-deciding it, and so the `architecture.md` TBD can be closed when `/implement`
next edits that file.

**Impact.** Determined FR-8 and, through it, the renaming recorded in A-10 below. Determined that
`ui/bff` is a component in its own right rather than a detail of `ui/portal`. Left Q-1 open —
the one part of the user's statement that did not resolve for server-to-server calls — which
C-7 below now answers.

---

### C-3 — How deep does the EF Core setup go with no entities to model?

**Ambiguity.** `architecture.md` states the data model is empty by design and the first feature
spec introduces the first entities. That leaves it undetermined whether the scaffold proves the
migration toolchain at all, and whether the `Migrate` command row can be filled.

**Options presented.**
1. Empty initial migration plus a migrate command — creates the SQLite file and
   migration-history table, proving the toolchain and settling the connection-string default;
   costs one empty migration in history.
2. DbContext only, no migration — the first entity-bearing spec creates the initial migration;
   `Migrate` and `Seed` stay `not yet defined` and the toolchain is unproven.
3. Add a placeholder entity — gives a non-empty schema to test against, but puts a non-domain
   table in the database that a later spec must drop, which under SQLite is a full table rebuild.

**Answer.** Option 1 — "Empty initial migration (Recommended). As you proposed."

**Impact.** Produced FR-9 and AC-8, AC-9. Kept `entities: []` in the frontmatter and left the
`architecture.md` ER diagram empty. Option 3 was rejected specifically because dropping a
placeholder table later is a table rebuild under SQLite, per the constraint in `architecture.md`.

---

### C-4 — Test frameworks and how many suites ship

**Ambiguity.** `tech-stack.md` and `coding-standards.md` both record xUnit and Vitest plus
Testing Library as *assumptions*, not decisions. Creating test projects converts the assumption
into a commitment every later spec inherits, so it needed confirming rather than inheriting
silently.

**Options presented.**
1. Confirm both, three suites — backend unit, backend integration (in-process host, real HTTP)
   and frontend component, each with one real passing test; fills both `Test` command rows.
2. Confirm both, two suites — one per side, no integration project; `Test (integration)` stays
   `not yet defined` and the first API spec pays to stand it up.
3. A different framework — name it.

**Answer.** Option 1 — "Confirm both; three suites (Recommended). xUnit and Vitest + Testing
Library confirmed and promoted from assumption to decision."

**Impact.** Promoted both from assumption to decision in `tech-stack.md` and
`coding-standards.md`. Produced FR-10, FR-11 and AC-17, AC-18, AC-19. AC-18 additionally carries
the `coding-standards.md` rule that each database test gets its own database file rather than a
shared one, given the single-writer constraint.

---

### C-5 — Are linting and formatting in scope?

**Ambiguity.** `coding-standards.md` states that its hand-written style rules should be *deleted*
once linters land, and that no linter is configured yet. Whether that happens now or later
determines whether those rules get written twice.

**Options presented.**
1. Both sides — `.editorconfig` plus `dotnet format`, and ESLint plus Prettier; `Lint` and
   `Format` rows filled, superseded rules removed from `coding-standards.md`.
2. Frontend only — ESLint ships with Next.js anyway; backend formatting stays a prose rule
   nobody enforces.
3. Neither — both command rows stay `not yet defined`.

**Answer.** Option 1 — "Both sides (Recommended). `.editorconfig` + `dotnet format`, ESLint +
Prettier. FR-14 is in. Fill the `Lint`/`Format` rows and remove the superseded prose rules from
`coding-standards.md`."

**Impact.** Produced FR-14, FR-15 and AC-22, AC-23, AC-24. The `coding-standards.md` edit is
`/implement`'s to make, per `meta-maintenance.md` §6 — this stage may not write to `meta/`.

---

### C-6 — Styling approach for the Next.js application

**Ambiguity.** `tech-stack.md` assigns styling to "the first UI spec". Unlike the other deferred
frontend choices, its cost grows with every spec that ships before it is settled, because
retrofitting a styling system touches every component written in the interim.

**Options presented.**
1. Tailwind CSS now — every later UI spec inherits one approach; adds a build-step dependency.
2. CSS Modules now — built into Next.js, zero extra dependency, more hand-written CSS later.
3. Defer as the blueprint says — the scaffold ships unstyled and the retrofit cost lands on
   whichever spec decides.

**Answer.** Option 1 — "Tailwind CSS now (Recommended). Settled here rather than deferred."

**Impact.** Amends the `tech-stack.md` Frontend table row from TBD to Tailwind CSS. The scaffold
makes it available and demonstrably applied on the landing page; no design system, theme or
layout shell is in scope.

---

## Round 2 — 2026-08-05

### C-7 — Must server-rendered code also route through the proxy handler? (resolves Q-1)

**Ambiguity.** The user's C-2 statement — "fe should always invoke be through a handler" — is
unambiguous for browser traffic and silent on server-to-server traffic. Taken literally it would
mean a Server Component fetching from the frontend process's own route handler: the Next.js
process calling itself over HTTP to reach a third process. Taken loosely it would mean
server-rendered code calls the backend directly, which risks two independent call-construction
sites and therefore two places `0002` must attach a token.

**Options presented.**
1. Server-rendered code calls the backend directly server-to-server — no self-hop, but needs a
   shared mechanism or token attachment fragments across two call sites.
2. Server-rendered code also goes through the proxy handler — one uniform path, at the cost of
   the frontend process making an HTTP request to itself on every render.

**Answer.** The user, verbatim:

> for the question yes for ssr invoke server to server but use a generic function which appends
> the tokens ( i mean like a server invoke where even proxy invoke be through it)

Option 1, with a structural condition that resolves its only drawback. There is one shared
server-side invoke function that attaches tokens and is the single place an outbound call to the
backend is constructed. It has two callers: server-rendered code calls it directly, and the
proxy handler also calls the backend through it rather than constructing its own request.

**Impact.** The browser invariant from C-2 is unchanged — the browser still reaches the backend
only via the proxy handler and still never touches the API origin. What changed is that the
proxy handler is no longer the sole path to the backend, and no longer the place tokens are
attached; both properties move to the shared function. The handler's job narrows to being the
browser's entry point.

Produced FR-16 and FR-17, and AC-26 through AC-30. Amended FR-7 so the walking skeleton
demonstrates both routes, and amended AC-12 and AC-15 to name the browser route specifically.
Added E-9 (server-render failure, a failure mode the browser route does not exercise) and E-10
(a call constructed outside the shared function). Amended A-11, and the C-2a record above, which
previously named the handler as the token-attachment point. Closed Q-1 and D-2.

`ui/bff` was re-derived against this answer and deliberately kept as one coarse path covering
both the handler and the invoke function; reasoning in A-9.

---

## Assumptions Made Without Asking

Ambiguities resolved by judgement rather than by asking, because a reasonable default existed
and the alternatives would not have changed the work materially. Listed so they can be
challenged. A-1 to A-8 were put to the user in Phase 1 and confirmed as stated.

| # | Assumption | Default chosen | Reversal cost if wrong |
|---|---|---|---|
| A-1 | Both deployables live in this one repository | Monorepo, per the Repository Layout in `tech-stack.md` | High, but already implied by the blueprint |
| A-2 | Versions are pinned exactly rather than by range | SDK version file, exact package versions, committed lockfiles, no floating ranges | Low — a manifest edit |
| A-3 | TypeScript strictness is set from the first commit | Strict mode on | Low now, high after any TypeScript is written |
| A-4 | Ignore rules cover more than the required minimum | Also local environment files, user-secrets artifacts, IDE directories and OS cruft | Trivial |
| A-5 | The cross-cutting mechanisms named in `architecture.md` are wired at scaffold | ProblemDetails handler, structured logging, typed configuration binding | Low — one file each, and awkward to retrofit into every endpoint later |
| A-6 | SQLite connection settings follow the stated mitigation | WAL enabled, busy timeout set | Trivial |
| A-7 | Seed data is not part of scaffolding | `Seed` command row stays `not yet defined` | Trivial |
| A-8 | Deployment tooling is not produced | No CI workflow, container definition or deployment configuration | Trivial |
| A-9 | Component paths, revised after the proxy came into scope and re-derived again after C-7 | `infra/build`, `api/system`, `service/system`, `db/core`, `ui/bff`, `ui/portal`. `ui/bff` covers both the proxy handler and the shared invoke function as one coarse path: `conventions.md` §5 asks for coarse logical addresses, "backend-for-frontend" is the standard name for exactly this layer including its server-side client, and the two parts would almost always be cited together. Recorded so a reader does not mistake `ui/bff` for the route handler alone — the Impacted Components table states the wider scope explicitly. `ui/staff` is excluded: the scaffold gives it an empty route group and no surface | Medium — these are permanent join keys, and renaming one after later specs cite it means editing those specs |
| A-10 | The backend base URL becomes server-only configuration | `NEXT_PUBLIC_API_BASE_URL` is renamed to a non-public key | Low now, higher later. Follows necessarily from FR-8: with the browser unable to reach the API origin, the base URL is a backend-only value, and `coding-standards.md` forbids backend-only values in a client-visible prefix. `tech-stack.md` explicitly invited the scaffolding spec to "confirm or rename" these keys |
| A-11 | The landing page demonstrates both routes, not one (revised after C-7) | A Server Component fetches status directly through the shared invoke function, and embeds a client component that fetches the same status through the proxy handler; both results render, each labelled with its route | Low. The client component is still justified against the "Server Components by default" rule, because the loading and error states `coding-standards.md` mandates are themselves the interactivity. Only demonstrating one route would leave the other unproven, and `0002` must attach tokens for both |
| A-12 | No CORS policy is configured on the backend | None | Trivial. FR-8 leaves no cross-origin browser request to permit; the first spec that introduces one owns the policy |

## Deferred

Questions raised but explicitly postponed, with where they were recorded.

| # | Question | Deferred to |
|---|---|---|
| D-1 | Identity endpoints, JWT issuance, NextAuth, role policies, login and refresh-token rotation | Spec `0002` — architecture decided in C-2a and C-7, not re-openable. `0002` fills the attachment point FR-16 establishes; it should not need to move a call site |
| ~~D-2~~ | ~~Whether server-rendered code must also route through the proxy handler~~ | Resolved, not deferred — answered by the user in C-7. `spec.md` now carries no open questions |
| D-3 | Validation library | First API spec, per `tech-stack.md` — nothing in a scaffold has a DTO to validate |
| D-4 | Server-state and forms libraries | First UI spec, per `tech-stack.md` — nothing in a scaffold has server data to cache or a form to submit |
| D-5 | Object storage for CVs and attachments (`shared/storage`) | The first spec with an upload surface |
| D-6 | Transactional email provider | The spec implementing candidate registration or password reset |
| D-7 | Hosting, deployment target and CI | Deferred by explicit user decision; no spec may pre-empt it |

## Related Specs

None — this is the first spec touching these components.
