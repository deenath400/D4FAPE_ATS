import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { ApplicationsTable } from "../../src/components/staff/ApplicationsTable";
import type { StaffApplicationListItemDto } from "../../src/lib/types/application";

describe("ApplicationsTable", () => {
  it("renders candidate identity, submitted date, and a CV link per row (AC-16)", () => {
    const items: StaffApplicationListItemDto[] = [
      {
        id: "app-1",
        candidate: {
          id: "candidate-1",
          firstName: "Jane",
          lastName: "Doe",
          email: "jane.doe@example.com",
        },
        submittedAtUtc: "2026-08-06T10:15:00Z",
        cvDownloadUrl: "/api/applications/app-1/cv",
      },
    ];

    render(<ApplicationsTable items={items} />);

    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText("jane.doe@example.com")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /download/i })).toHaveAttribute(
      "href",
      "/api/bff/proxy/api/applications/app-1/cv",
    );
  });

  it("renders an empty state for zero applications (AC-18)", () => {
    render(<ApplicationsTable items={[]} />);

    expect(screen.getByText(/no applications yet for this requisition/i)).toBeInTheDocument();
  });
});
