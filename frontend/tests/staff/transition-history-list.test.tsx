import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { TransitionHistoryList } from "../../src/components/staff/TransitionHistoryList";
import type { StageTransitionDto } from "../../src/lib/types/pipeline";

describe("TransitionHistoryList", () => {
  it("renders an empty state when there are no transitions yet (AC-21)", () => {
    render(<TransitionHistoryList items={[]} />);

    expect(screen.getByText(/no transitions yet/i)).toBeInTheDocument();
  });

  it("renders a chronological list of move transitions (AC-20)", () => {
    const items: StageTransitionDto[] = [
      {
        id: "t-1",
        applicationId: "app-1",
        fromStageId: "stage-1",
        fromStageName: "Applied",
        toStageId: "stage-2",
        toStageName: "Screening",
        kind: "move",
        actorDisplayLabel: "Jane Recruiter",
        note: null,
        occurredAtUtc: "2026-08-06T09:00:00Z",
      },
      {
        id: "t-2",
        applicationId: "app-1",
        fromStageId: "stage-2",
        fromStageName: "Screening",
        toStageId: "stage-3",
        toStageName: "Interview",
        kind: "move",
        actorDisplayLabel: "Jane Recruiter",
        note: null,
        occurredAtUtc: "2026-08-07T09:00:00Z",
      },
    ];

    render(<TransitionHistoryList items={items} />);

    expect(screen.getByText("Applied → Screening")).toBeInTheDocument();
    expect(screen.getByText("Screening → Interview")).toBeInTheDocument();
  });

  it("renders the note for a rejection (AC-30)", () => {
    const items: StageTransitionDto[] = [
      {
        id: "t-1",
        applicationId: "app-1",
        fromStageId: "stage-2",
        fromStageName: "Screening",
        toStageId: null,
        toStageName: null,
        kind: "reject",
        actorDisplayLabel: "Jane Recruiter",
        note: "Not enough backend depth",
        occurredAtUtc: "2026-08-06T09:00:00Z",
      },
    ];

    render(<TransitionHistoryList items={items} />);

    expect(screen.getByText("Rejected from Screening")).toBeInTheDocument();
    expect(screen.getByText("Not enough backend depth")).toBeInTheDocument();
  });
});
