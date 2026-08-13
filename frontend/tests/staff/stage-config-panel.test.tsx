import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { StageConfigPanel } from "../../src/components/staff/StageConfigPanel";
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
];

describe("StageConfigPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders every stage and hides write affordances when canWrite is false", () => {
    render(<StageConfigPanel requisitionId="req-1" stages={stages} canWrite={false} />);

    expect(screen.getByText("Applied")).toBeInTheDocument();
    expect(screen.getByText("Screening")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /remove/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /add stage/i })).not.toBeInTheDocument();
  });

  it("shows a validation error for an empty new stage name (AC-1)", async () => {
    render(<StageConfigPanel requisitionId="req-1" stages={stages} canWrite={true} />);

    fireEvent.click(screen.getByRole("button", { name: /add stage/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(/stage name is required/i);
    });
  });

  it("shows a 409 banner on a duplicate stage name (AC-31)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({
        detail: "A stage with this name already exists.",
        code: "stage.add.duplicate-name",
      }),
    } as Response);

    render(<StageConfigPanel requisitionId="req-1" stages={stages} canWrite={true} />);

    fireEvent.change(screen.getByLabelText(/new stage name/i), {
      target: { value: "Screening" },
    });
    fireEvent.click(screen.getByRole("button", { name: /add stage/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent("A stage with this name already exists.");
    });
    expect(mockRefresh).not.toHaveBeenCalled();
  });

  it("shows an error banner when removing an occupied stage returns 409 (AC-6)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({
        detail: "This stage still has applications assigned to it.",
        code: "stage.remove.occupied",
      }),
    } as Response);

    render(<StageConfigPanel requisitionId="req-1" stages={stages} canWrite={true} />);

    const removeButtons = screen.getAllByRole("button", { name: /^remove$/i });
    fireEvent.click(removeButtons[0]);

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(
        "This stage still has applications assigned to it.",
      );
    });
    expect(mockRefresh).not.toHaveBeenCalled();
    // The stage remains listed — the failed remove did not alter local state.
    expect(screen.getByText("Applied")).toBeInTheDocument();
  });

  it("adds a stage and refreshes on success", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 201,
      json: async () => ({ id: "stage-3", requisitionId: "req-1", name: "Offer", sortOrder: 2 }),
    } as Response);

    render(<StageConfigPanel requisitionId="req-1" stages={stages} canWrite={true} />);

    fireEvent.change(screen.getByLabelText(/new stage name/i), { target: { value: "Offer" } });
    fireEvent.click(screen.getByRole("button", { name: /add stage/i }));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/bff/proxy/requisitions/req-1/stages",
        expect.objectContaining({ method: "POST" }),
      );
      expect(mockRefresh).toHaveBeenCalled();
    });
  });
});
