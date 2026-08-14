import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { ScreeningReportModal } from "../../src/components/staff/ScreeningReportModal";
import type { ScreeningReportDto } from "../../src/lib/types/screening";

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    refresh: vi.fn(),
  }),
}));

describe("ScreeningReportModal", () => {
  const mockReport: ScreeningReportDto = {
    applicationId: "app-123",
    score: 85,
    recommendation: "Advance",
    summary: "Strong candidate with solid C# experience.",
    strengths: ["5+ years C# .NET", "Strong database background"],
    concerns: ["No prior ATS experience"],
    status: "Completed",
    failureReason: null,
    screenedAtUtc: "2026-08-14T10:00:00Z",
  };

  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation((url: string) => {
        if (url.includes("/screening-report")) {
          return Promise.resolve({
            ok: true,
            json: () => Promise.resolve(mockReport),
          });
        }
        if (url.includes("/screen")) {
          return Promise.resolve({
            ok: true,
            json: () => Promise.resolve({ ...mockReport, score: 90 }),
          });
        }
        return Promise.reject(new Error(`Unhandled URL: ${url}`));
      })
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("does not render when isOpen is false", () => {
    const { container } = render(
      <ScreeningReportModal
        applicationId="app-123"
        candidateName="Jane Doe"
        isOpen={false}
        onClose={vi.fn()}
      />
    );

    expect(container.firstChild).toBeNull();
  });

  it("fetches and displays report data when isOpen is true", async () => {
    render(
      <ScreeningReportModal
        applicationId="app-123"
        candidateName="Jane Doe"
        isOpen={true}
        onClose={vi.fn()}
      />
    );

    expect(screen.getByText(/loading screening analysis/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("AI Screening Report")).toBeInTheDocument();
      expect(screen.getByText("Applicant: Jane Doe")).toBeInTheDocument();
      expect(screen.getByText("Strong candidate with solid C# experience.")).toBeInTheDocument();
      expect(screen.getByText("5+ years C# .NET")).toBeInTheDocument();
      expect(screen.getByText("No prior ATS experience")).toBeInTheDocument();
    });
  });

  it("calls onClose when Close button is clicked", async () => {
    const handleClose = vi.fn();
    render(
      <ScreeningReportModal
        applicationId="app-123"
        candidateName="Jane Doe"
        isOpen={true}
        onClose={handleClose}
      />
    );

    await waitFor(() => {
      expect(screen.getByText("AI Screening Report")).toBeInTheDocument();
    });

    const closeButton = screen.getByRole("button", { name: "Close" });
    fireEvent.click(closeButton);

    expect(handleClose).toHaveBeenCalledTimes(1);
  });
});
