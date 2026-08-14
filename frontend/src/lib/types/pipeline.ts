// Mirrors `docs/specs/0005-pipeline-progression/plan/api.md` §4 exactly — keep these two in
// sync by hand; there is no shared schema generator in this project yet.

export type StageDto = {
  id: string;
  requisitionId: string;
  name: string;
  sortOrder: number;
};

export type AddStageRequestDto = { name: string; position?: number };
export type RenameStageRequestDto = { name: string };
export type ReorderStagesRequestDto = { stageIds: string[] };

export type MoveApplicationRequestDto = {
  targetStageId: string;
  expectedCurrentStageId: string;
  note?: string;
};
export type RejectApplicationRequestDto = { note?: string };

export type StageTransitionDto = {
  id: string;
  applicationId: string;
  fromStageId: string | null;
  fromStageName: string;
  toStageId: string | null;
  toStageName: string | null;
  kind: "move" | "reject";
  actorDisplayLabel: string;
  note: string | null;
  occurredAtUtc: string; // ISO-8601 UTC
};

export type ApplicationTransitionDto = {
  applicationId: string;
  requisitionId: string;
  currentStageId: string;
  currentStageName: string;
  isRejected: boolean;
  transition: StageTransitionDto;
};

export type PipelineBoardApplicationDto = {
  applicationId: string;
  candidateId: string;
  candidateFirstName: string;
  candidateLastName: string;
  candidateEmail: string;
  submittedAtUtc: string;
  screeningScore?: number | null;
  screeningRecommendation?: "Advance" | "Review" | null;
  screeningStatus?: "Pending" | "Completed" | "Failed" | null;
};

export type PipelineStageGroupDto = {
  stageId: string;
  stageName: string;
  sortOrder: number;
  count: number;
  applications: PipelineBoardApplicationDto[];
};

export type PipelineRejectedGroupDto = {
  count: number;
  applications: PipelineBoardApplicationDto[];
};

export type PipelineBoardDto = {
  requisitionId: string;
  stages: PipelineStageGroupDto[];
  rejected: PipelineRejectedGroupDto;
};

export type ProblemDetails = {
  type: string;
  title: string;
  status: number;
  code: string;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
  actualCurrentStageId?: string; // only on application.move.conflict
  actualCurrentStageName?: string; // only on application.move.conflict
};
