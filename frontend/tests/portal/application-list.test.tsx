import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { ApplicationList } from "../../src/components/portal/ApplicationList";
import type { CandidateApplicationListItemDto } from "../../src/lib/types/application";

describe("ApplicationList", () => {
  it("renders an empty state for zero applications (AC-13)", () => {
    render(<ApplicationList items={[]} />);

    expect(screen.getByText(/haven.t applied to any roles yet/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /browse open roles/i })).toHaveAttribute(
      "href",
      "/jobs",
    );
  });

  it("renders the Requisition title, submitted date, and a CV link per row (AC-12)", () => {
    const items: CandidateApplicationListItemDto[] = [
      {
        id: "app-1",
        requisitionId: "req-1",
        requisitionTitle: "Senior Backend Engineer",
        submittedAtUtc: "2026-08-06T10:15:00Z",
        cvDownloadUrl: "/api/applications/app-1/cv",
      },
      {
        id: "app-2",
        requisitionId: "req-2",
        requisitionTitle: "Product Designer",
        submittedAtUtc: "2026-08-05T09:00:00Z",
        cvDownloadUrl: "/api/applications/app-2/cv",
      },
    ];

    render(<ApplicationList items={items} />);

    expect(screen.getByText("Senior Backend Engineer")).toBeInTheDocument();
    expect(screen.getByText("Product Designer")).toBeInTheDocument();

    const downloadLinks = screen.getAllByRole("link", { name: /download cv/i });
    expect(downloadLinks).toHaveLength(2);
    expect(downloadLinks[0]).toHaveAttribute("href", "/api/bff/proxy/api/applications/app-1/cv");
  });
});
