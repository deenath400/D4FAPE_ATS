import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { MoveApplicationControl } from "../../src/components/staff/MoveApplicationControl";
import type { StageDto } from "../../src/lib/types/pipeline";

const mockRefresh = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    refresh: mockRefresh,
  }),
}));

const stages: StageDto[] = [
  { id: "stage-1", requisitionId: "req-1", name: "Applied", sortOrder: 0 },
  { id: "stage-2", requisitionId: "req-1", name: "Screening", sortOrder: 1 },
  { id: "stage-3", requisitionId: "req-1", name: "Interview", sortOrder: 2 },
];

describe("MoveApplicationControl", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("moves the application and refreshes on success (AC-11)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({}),
    } as Response);

    render(
      <MoveApplicationControl applicationId="app-1" currentStageId="stage-1" stages={stages} />,
    );

    fireEvent.click(screen.getByRole("button", { name: /^move$/i }));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/bff/proxy/applications/app-1/move",
        expect.objectContaining({ method: "POST" }),
      );
      expect(mockRefresh).toHaveBeenCalled();
    });
  });

  it("shows the actual current stage on a 409 conflict and refreshes (AC-29)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({
        detail: "This application has already moved.",
        code: "application.move.conflict",
        actualCurrentStageId: "stage-3",
        actualCurrentStageName: "Interview",
      }),
    } as Response);

    render(
      <MoveApplicationControl applicationId="app-1" currentStageId="stage-1" stages={stages} />,
    );

    fireEvent.click(screen.getByRole("button", { name: /^move$/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(
        "This application already moved to Interview. Refreshing.",
      );
      expect(mockRefresh).toHaveBeenCalled();
    });
  });
});
