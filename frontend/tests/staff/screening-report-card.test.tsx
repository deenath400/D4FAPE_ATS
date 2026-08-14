import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { ScreeningReportCard } from "../../src/components/staff/ScreeningReportCard";
import type { ScreeningReportDto } from "../../src/lib/types/screening";

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    refresh: vi.fn(),
  }),
}));

describe("ScreeningReportCard", () => {
  const baseReport: ScreeningReportDto = {
    applicationId: "app-456",
    score: 80,
    recommendation: "Advance",
    summary: "Solid candidate background.",
    strengths: ["C# programming", "Fast learner"],
    concerns: ["None"],
    status: "Completed",
    failureReason: null,
    screenedAtUtc: "2026-08-14T11:00:00Z",
  };

  it("renders report summary and strengths", () => {
    render(<ScreeningReportCard applicationId="app-456" report={baseReport} />);

    expect(screen.getByText("AI Screening Analysis")).toBeInTheDocument();
    expect(screen.getByText("Solid candidate background.")).toBeInTheDocument();
    expect(screen.getByText("C# programming")).toBeInTheDocument();
    expect(screen.queryByText("Evaluation Breakdown")).toBeNull();
  });

  it("renders category breakdown scores when provided", () => {
    const reportWithCategories: ScreeningReportDto = {
      ...baseReport,
      skillsScore: 88,
      experienceScore: 75,
      educationScore: 90,
    };

    render(<ScreeningReportCard applicationId="app-456" report={reportWithCategories} />);

    expect(screen.getByText("Evaluation Breakdown")).toBeInTheDocument();
    expect(screen.getByText("Skills Fit")).toBeInTheDocument();
    expect(screen.getByText("88%")).toBeInTheDocument();
    expect(screen.getByText("Experience Fit")).toBeInTheDocument();
    expect(screen.getByText("75%")).toBeInTheDocument();
    expect(screen.getByText("Education Fit")).toBeInTheDocument();
    expect(screen.getByText("90%")).toBeInTheDocument();
  });
});
