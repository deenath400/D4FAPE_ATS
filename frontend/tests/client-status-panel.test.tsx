import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { ClientStatusPanel } from "../src/components/ClientStatusPanel";

describe("ClientStatusPanel", () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  it("shows a loading state before the fetch resolves", async () => {
    global.fetch = vi.fn().mockImplementation(
      () => new Promise(() => {}), // never resolves
    );

    render(<ClientStatusPanel />);

    expect(screen.getByTestId("status-skeleton")).toBeInTheDocument();
    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("renders the browser-retrieved status on success", async () => {
    const mockStatus = {
      version: "1.0.0",
      database: {
        reachable: true,
        schemaCurrent: true,
      },
    };

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockStatus,
    } as Response);

    render(<ClientStatusPanel />);

    await waitFor(() => {
      expect(screen.getByTestId("client-status-panel")).toBeInTheDocument();
    });

    expect(screen.getByText("Browser-retrieved Status")).toBeInTheDocument();
    expect(screen.getByText("1.0.0")).toBeInTheDocument();
    expect(screen.getAllByText("Yes").length).toBe(2);
  });

  it("renders an error state without the backend URL on failure", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 502,
      json: async () => ({ message: "Unable to reach the backend service." }),
    } as Response);

    render(<ClientStatusPanel />);

    await waitFor(() => {
      expect(screen.getByText("Unable to reach the backend service.")).toBeInTheDocument();
    });

    const renderedText = screen.getByTestId("client-status-panel").textContent ?? "";
    expect(renderedText).not.toContain("localhost");
    expect(renderedText).not.toContain("http");
  });
});
