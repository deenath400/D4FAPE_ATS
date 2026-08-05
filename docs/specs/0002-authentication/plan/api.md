# API Design — 0002 User Authentication and Refresh Token Flow

**Spec:** `../spec.md` · **Updated:** 2026-08-05

> **Convention inheritance.** Before designing anything here, read `plan/api.md` of the Tier-1 specs selected per `spec-kit/context-loading.md`. Reuse URL shapes, error envelope, and auth header conventions established by Spec `0001`.

---

## 1. Conventions In Force

Restate the inherited conventions this spec follows, so the file is self-contained.

| Concern | Convention | Established by |
|---|---|---|
| Base path | `/api` | 0001 |
| Casing | camelCase JSON bodies, kebab-case paths | 0001 |
| Auth Header | `Authorization: Bearer <jwt>` | 0001 establishes header name; 0002 populates & validates JWT |
| Errors | RFC 7807 ProblemDetails for every non-2xx response, `code` field for machine-readable reason, `traceId` from request trace identifier | 0001 |
| Dates | ISO-8601 UTC with `Z` | 0001 |
| Pagination | Not applicable to auth endpoints | — |
| Idempotency | `POST /api/auth/logout` and `POST /api/auth/refresh` handle state transitions | 0002 |

---

## 2. Endpoint Summary

| # | Method | Path | Purpose | Auth | AC |
|---|---|---|---|---|---|
| 1 | POST | `/api/auth/register` | Register new candidate account | Anonymous | AC-1, AC-2, AC-3 |
| 2 | POST | `/api/auth/login` | Authenticate user credentials and issue token pair | Anonymous | AC-4, AC-5, AC-21, AC-23 |
| 3 | POST | `/api/auth/refresh` | Exchange active refresh token for new access & rotated refresh token | Anonymous | AC-6, AC-7, AC-22 |
| 4 | POST | `/api/auth/logout` | Revoke active refresh token | Bearer Token | AC-8 |
| 5 | GET | `/api/auth/me` | Return current authenticated user principal details | Bearer Token | AC-9, AC-10, AC-20 |

---

## 3. Endpoint Detail

### 3.1 `POST /api/auth/register`

**Purpose.** Registers a new candidate account, hashes password via ASP.NET Core Identity, and assigns the `Candidate` role by default.

**Path parameters.** None.

**Request body**:

```json
{
  "email": "candidate@example.com",
  "password": "SecurePassword123!",
  "firstName": "Ada",
  "lastName": "Lovelace"
}
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `email` | string | Yes | Valid email format |
| `password` | string | Yes | Min 8 chars, 1 uppercase, 1 lowercase, 1 digit, 1 non-alphanumeric |
| `firstName` | string | Yes | Max 100 characters |
| `lastName` | string | Yes | Max 100 characters |

**Responses**:

| Status | Body | When |
|---|---|---|
| 201 | `UserDto` | Candidate account successfully created |
| 400 | ProblemDetails | Validation failure (e.g. weak password, missing fields) |
| 409 | ProblemDetails (`code: "auth.register.duplicate-email"`) | Email address already registered |

**Success Example (201)**:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "candidate@example.com",
  "firstName": "Ada",
  "lastName": "Lovelace",
  "roles": ["Candidate"]
}
```

**Error Example (409)**:

```json
{
  "type": "https://d4fape.ats/errors/duplicate-email",
  "title": "Conflict",
  "status": 409,
  "code": "auth.register.duplicate-email",
  "detail": "An account with this email address already exists.",
  "traceId": "00-4b...-01"
}
```

**Side effects.** Creates `ApplicationUser` in `AspNetUsers` and inserts `Candidate` join record into `AspNetUserRoles`.

**Idempotency.** Non-idempotent. Retrying with the same email returns 409 Conflict.

---

### 3.2 `POST /api/auth/login`

**Purpose.** Authenticates user credentials, generates a JWT access token (15-minute expiry), creates a rotated refresh token (7-day expiry), and returns token pair.

**Request body**:

```json
{
  "email": "candidate@example.com",
  "password": "SecurePassword123!"
}
```

**Responses**:

| Status | Body | When |
|---|---|---|
| 200 | `AuthResponseDto` | Successful authentication |
| 400 | ProblemDetails | Missing email or password |
| 401 | ProblemDetails (`code: "auth.login.invalid-credentials"`) | Incorrect email or password |

**Success Example (200)**:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "4a7f8e9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "candidate@example.com",
    "firstName": "Ada",
    "lastName": "Lovelace",
    "roles": ["Candidate"]
  }
}
```

---

### 3.3 `POST /api/auth/refresh`

**Purpose.** Validates presented refresh token, revokes it, issues new access and rotated refresh token pair.

**Request body**:

```json
{
  "refreshToken": "4a7f8e9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f"
}
```

**Responses**:

| Status | Body | When |
|---|---|---|
| 200 | `AuthResponseDto` | Successful refresh and token rotation |
| 400 | ProblemDetails | Missing refreshToken field |
| 401 | ProblemDetails (`code: "auth.refresh.invalid-token"` or `"auth.refresh.token-revoked"`) | Token invalid, expired, or revoked |

---

### 3.4 `POST /api/auth/logout`

**Purpose.** Revokes active refresh token for the authenticated user session.

**Header**: `Authorization: Bearer <accessToken>`

**Request body**:

```json
{
  "refreshToken": "4a7f8e9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f"
}
```

**Responses**:

| Status | Body | When |
|---|---|---|
| 200 | `{ "message": "Successfully logged out." }` | Refresh token revoked |
| 401 | ProblemDetails | Invalid or missing access token |

---

### 3.5 `GET /api/auth/me`

**Purpose.** Returns details of the currently authenticated `ClaimsPrincipal`.

**Header**: `Authorization: Bearer <accessToken>`

**Responses**:

| Status | Body | When |
|---|---|---|
| 200 | `UserDto` | Valid bearer token supplied |
| 401 | ProblemDetails | Missing or invalid bearer token |

---

## 4. Shared Schemas

```ts
type AuthResponseDto = {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
  user: UserDto;
};

type UserDto = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
};

type ProblemDetails = {
  type: string;
  title: string;
  status: number;
  code: string;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId: string;
};
```

---

## 5. Authorization Matrix

| Endpoint | Anonymous | Candidate | Recruiter | Hiring Manager |
|---|---|---|---|---|
| `POST /api/auth/register` | Allowed | Allowed | Allowed | Allowed |
| `POST /api/auth/login` | Allowed | Allowed | Allowed | Allowed |
| `POST /api/auth/refresh` | Allowed | Allowed | Allowed | Allowed |
| `POST /api/auth/logout` | Denied (401) | Allowed | Allowed | Allowed |
| `GET /api/auth/me` | Denied (401) | Allowed | Allowed | Allowed |

---

## 6. Events Published

None.

---

## 7. Deviations From Inherited Conventions

None — strictly adheres to base path `/api`, camelCase JSON properties, RFC 7807 ProblemDetails, and bearer authentication established in Spec `0001`.

---

## Related Specs

| Spec | Tier | Why loaded |
|---|---|---|
| `0001` (Project Scaffolding) | Tier 1 | Inherited `/api` base path, RFC 7807 ProblemDetails error format, and bearer header conventions. |
