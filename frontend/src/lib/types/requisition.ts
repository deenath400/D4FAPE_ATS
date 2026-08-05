// Mirrors `docs/specs/0003-requisition-management/plan/api.md` §4 exactly — keep these two in
// sync by hand; there is no shared schema generator in this project yet.

export type RequisitionStatus = "draft" | "published" | "closed";

export type RequisitionDto = {
  id: string;
  title: string;
  description: string;
  status: RequisitionStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type PublicRequisitionDto = {
  id: string;
  title: string;
  description: string;
  updatedAtUtc: string;
};

export type Paged<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};

export type CreateRequisitionRequestDto = {
  title: string;
  description: string;
};

export type UpdateRequisitionRequestDto = {
  title: string;
  description: string;
};
