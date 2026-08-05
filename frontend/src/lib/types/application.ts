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
};

export type StaffApplicationListItemDto = {
  id: string;
  candidate: { id: string; firstName: string; lastName: string; email: string };
  submittedAtUtc: string;
  cvDownloadUrl: string;
};
