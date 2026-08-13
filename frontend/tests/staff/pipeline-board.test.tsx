import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { PipelineBoard } from "../../src/components/staff/PipelineBoard";
import type { PipelineBoardDto } from "../../src/lib/types/pipeline";

describe("PipelineBoard", () => {
  it("renders every configured stage column at zero count for an empty requisition (AC-19)", () => {
    const board: PipelineBoardDto = {
      requisitionId: "req-1",
      stages: [
        { stageId: "stage-1", stageName: "Applied", sortOrder: 0, count: 0, applications: [] },
        { stageId: "stage-2", stageName: "Screening", sortOrder: 1, count: 0, applications: [] },
      ],
      rejected: { count: 0, applications: [] },
    };

    render(<PipelineBoard requisitionId="req-1" board={board} canWrite={false} />);

    expect(screen.getByText("Applied")).toBeInTheDocument();
    expect(screen.getByText("Screening")).toBeInTheDocument();
    expect(screen.getAllByText("0")).toHaveLength(3); // two stage columns + rejected column
  });

  it("groups applications by stage with counts and a separate Rejected column (AC-18)", () => {
    const board: PipelineBoardDto = {
      requisitionId: "req-1",
      stages: [
        {
          stageId: "stage-1",
          stageName: "Applied",
          sortOrder: 0,
          count: 1,
          applications: [
            {
              applicationId: "app-1",
              candidateId: "cand-1",
              candidateFirstName: "Jane",
              candidateLastName: "Doe",
              candidateEmail: "jane.doe@example.com",
              submittedAtUtc: "2026-08-06T09:00:00Z",
            },
          ],
        },
        { stageId: "stage-2", stageName: "Screening", sortOrder: 1, count: 0, applications: [] },
      ],
      rejected: {
        count: 1,
        applications: [
          {
            applicationId: "app-2",
            candidateId: "cand-2",
            candidateFirstName: "John",
            candidateLastName: "Smith",
            candidateEmail: "john.smith@example.com",
            submittedAtUtc: "2026-08-05T09:00:00Z",
          },
        ],
      },
    };

    render(<PipelineBoard requisitionId="req-1" board={board} canWrite={false} />);

    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText("Rejected")).toBeInTheDocument();
    expect(screen.getByText("John Smith")).toBeInTheDocument();
  });

  it("hides move/reject controls when canWrite is false", () => {
    const board: PipelineBoardDto = {
      requisitionId: "req-1",
      stages: [
        {
          stageId: "stage-1",
          stageName: "Applied",
          sortOrder: 0,
          count: 1,
          applications: [
            {
              applicationId: "app-1",
              candidateId: "cand-1",
              candidateFirstName: "Jane",
              candidateLastName: "Doe",
              candidateEmail: "jane.doe@example.com",
              submittedAtUtc: "2026-08-06T09:00:00Z",
            },
          ],
        },
      ],
      rejected: { count: 0, applications: [] },
    };

    render(<PipelineBoard requisitionId="req-1" board={board} canWrite={false} />);

    expect(screen.queryByRole("button", { name: /^move$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^reject$/i })).not.toBeInTheDocument();
  });
});
