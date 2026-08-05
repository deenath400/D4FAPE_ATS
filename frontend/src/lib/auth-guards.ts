// Pure role-check helpers — no NextAuth/Next.js imports, so both `middleware.ts` (Edge
// runtime) and Server Components can use them without pulling in unrelated dependencies.
// Mirrors the backend's `StaffOnly`/`RecruiterOnly` policy names (`AuthConstants.Roles`,
// spec 0002) without importing backend code across the `ui/*` boundary.

const STAFF_ROLES = ["Recruiter", "HiringManager"] as const;

/**
 * True when the given role list includes at least one staff role (Recruiter or
 * HiringManager) — mirrors the backend's `StaffOnly` policy (FR-9). Used by `middleware.ts`
 * to gate `/staff/*` (AC-14, AC-15) and by the Server Components that render its pages.
 */
export function isStaffRole(roles: readonly string[] | null | undefined): boolean {
  if (!roles || roles.length === 0) {
    return false;
  }
  return roles.some((role) => (STAFF_ROLES as readonly string[]).includes(role));
}

/**
 * True only for a Recruiter — the sole role permitted to create, edit, or transition a
 * Requisition (FR-8). Used to decide whether write-capable UI (the edit form, lifecycle
 * buttons) should render; the backend's `RecruiterOnly` policy remains the real boundary
 * (`coding-standards.md` — UI gating is a convenience, not a security control).
 */
export function isRecruiter(roles: readonly string[] | null | undefined): boolean {
  return !!roles?.includes("Recruiter");
}

/**
 * True only for a Candidate — the sole role permitted to apply to a Requisition or view its
 * own Applications (FR-6, spec 0004). Used by `middleware.ts` to gate `/applications/*` and by
 * the Server Components that render its pages; the backend's `CandidateOnly` policy remains
 * the real boundary (`coding-standards.md` — UI gating is a convenience, not a security
 * control).
 */
export function isCandidateRole(roles: readonly string[] | null | undefined): boolean {
  return !!roles?.includes("Candidate");
}
