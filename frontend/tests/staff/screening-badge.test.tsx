import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import React from "react";
import { ScreeningBadge } from "../../src/components/staff/ScreeningBadge";

describe("ScreeningBadge", () => {
  it("renders Advance recommendation with score", () => {
    render(<ScreeningBadge score={85} recommendation="Advance" status="Completed" />);

    expect(screen.getByText("85 · Advance")).toBeInTheDocument();
  });

  it("renders Review recommendation with score", () => {
    render(<ScreeningBadge score={65} recommendation="Review" status="Completed" />);

    expect(screen.getByText("65 · Review")).toBeInTheDocument();
  });

  it("renders Failed status", () => {
    render(<ScreeningBadge score={0} status="Failed" />);

    expect(screen.getByText("Screening Failed")).toBeInTheDocument();
  });

  it("renders Pending status with pulse animation", () => {
    render(<ScreeningBadge status="Pending" />);

    expect(screen.getByText("Screening...")).toBeInTheDocument();
  });

  it("renders nothing when no screening info is available", () => {
    const { container } = render(<ScreeningBadge />);

    expect(container.firstChild).toBeNull();
  });

  it("triggers onClick callback when provided", () => {
    const handleClick = vi.fn();
    render(
      <ScreeningBadge
        score={90}
        recommendation="Advance"
        status="Completed"
        onClick={handleClick}
      />
    );

    const button = screen.getByRole("button");
    fireEvent.click(button);

    expect(handleClick).toHaveBeenCalledTimes(1);
  });
});
