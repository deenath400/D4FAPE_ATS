import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { JobSearchForm } from "../../src/components/portal/JobSearchForm";
import { JobList } from "../../src/components/portal/JobList";
import type { PublicRequisitionDto } from "../../src/lib/types/requisition";

describe("JobSearchForm", () => {
  it("renders a GET form targeting /jobs with a keyword input (AC-16)", () => {
    render(<JobSearchForm />);

    const form = screen.getByRole("search");
    expect(form).toHaveAttribute("method", "get");
    expect(form).toHaveAttribute("action", "/jobs");

    const input = screen.getByLabelText(/search jobs/i);
    expect(input).toHaveAttribute("name", "keyword");
  });

  it("pre-fills the keyword input from defaultKeyword", () => {
    render(<JobSearchForm defaultKeyword="engineer" />);
    expect(screen.getByLabelText(/search jobs/i)).toHaveValue("engineer");
  });
});

describe("JobList", () => {
  it("renders an empty state when there are zero results (AC-17)", () => {
    render(<JobList items={[]} page={1} pageSize={20} total={0} keyword="nonexistent" />);
    expect(screen.getByText(/no roles match your search/i)).toBeInTheDocument();
  });

  it("renders job cards and pagination metadata for a filtered, paginated result (AC-20)", () => {
    const items: PublicRequisitionDto[] = [
      {
        id: "job-1",
        title: "Senior Engineer",
        description: "Build things.",
        updatedAtUtc: "2026-08-05T09:00:00Z",
      },
    ];

    render(<JobList items={items} page={2} pageSize={20} total={37} keyword="engineer" />);

    expect(screen.getByText("Senior Engineer")).toBeInTheDocument();
    expect(screen.getByText(/page 2 of 2/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /previous/i })).toHaveAttribute(
      "href",
      "/jobs?keyword=engineer&page=1",
    );
    expect(screen.queryByRole("link", { name: /^next$/i })).not.toBeInTheDocument();
  });
});
