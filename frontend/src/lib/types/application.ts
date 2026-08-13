// Mirrors `docs/specs/0004-application-submission/plan/api.md` §4 exactly — keep these two in
// sync by hand; there is no shared schema generator in this project yet.

export type ApplicationDto = {
  id: string;
  requisitionId: string;
  candidateId: string;
  submittedAtUtc: string;
  cv: { fileName: string; contentType: string; sizeBytes: number };
};

export type CandidateApplicationListItemDto = {
  id: string;
  requisitionId: string;
  requisitionTitle: string;
  submittedAtUtc: string;
  cvDownloadUrl: string; // backend-relative path; frontend prefixes /api/bff/proxy
  currentStageName: string; // 0005 FR-17 — retained even when isRejected is true
  isRejected: boolean; // 0005 FR-17 — the frontend shows a rejected indicator instead (AC-23)
};

export type StaffApplicationListItemDto = {
  id: string;
  candidate: { id: string; firstName: string; lastName: string; email: string };
  submittedAtUtc: string;
  cvDownloadUrl: string;
};
