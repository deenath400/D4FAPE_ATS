import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { RequisitionForm } from "../../src/components/staff/RequisitionForm";

const mockPush = vi.fn();
const mockRefresh = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: mockPush,
    refresh: mockRefresh,
  }),
}));

describe("RequisitionForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows a validation error and does not submit when title is empty", async () => {
    global.fetch = vi.fn();

    render(<RequisitionForm mode="create" />);

    fireEvent.change(screen.getByLabelText(/description/i), {
      target: { value: "We are looking for a great engineer." },
    });
    fireEvent.click(screen.getByRole("button", { name: /create requisition/i }));

    await waitFor(() => {
      expect(screen.getByText("Title is required.")).toBeInTheDocument();
    });
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it("creates a requisition and redirects to its detail page on success (AC-1)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 201,
      json: async () => ({
        id: "req-1",
        title: "Senior Backend Engineer",
        description: "We are looking for...",
        status: "draft",
        createdAtUtc: "2026-08-05T09:00:00Z",
        updatedAtUtc: "2026-08-05T09:00:00Z",
      }),
    } as Response);

    render(<RequisitionForm mode="create" />);

    fireEvent.change(screen.getByLabelText(/title/i), {
      target: { value: "Senior Backend Engineer" },
    });
    fireEvent.change(screen.getByLabelText(/description/i), {
      target: { value: "We are looking for..." },
    });
    fireEvent.click(screen.getByRole("button", { name: /create requisition/i }));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/bff/proxy/requisitions",
        expect.objectContaining({ method: "POST" }),
      );
      expect(mockPush).toHaveBeenCalledWith("/staff/requisitions/req-1");
    });
  });

  it("edits a draft requisition and refreshes on success (AC-3)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        id: "req-1",
        title: "Updated Title",
        description: "Updated description.",
        status: "draft",
        createdAtUtc: "2026-08-05T09:00:00Z",
        updatedAtUtc: "2026-08-05T10:00:00Z",
      }),
    } as Response);

    render(
      <RequisitionForm
        mode="edit"
        requisitionId="req-1"
        initialTitle="Old Title"
        initialDescription="Old description."
      />,
    );

    fireEvent.change(screen.getByLabelText(/title/i), { target: { value: "Updated Title" } });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/bff/proxy/requisitions/req-1",
        expect.objectContaining({ method: "PUT" }),
      );
      expect(mockRefresh).toHaveBeenCalled();
    });
  });

  it("shows a 409 error banner when editing a closed requisition (AC-5)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({
        detail: "A closed requisition cannot be edited.",
        code: "requisition.update.closed",
      }),
    } as Response);

    render(
      <RequisitionForm
        mode="edit"
        requisitionId="req-1"
        initialTitle="Closed Role"
        initialDescription="Closed description."
      />,
    );

    fireEvent.change(screen.getByLabelText(/title/i), { target: { value: "Still Closed Role" } });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(
        "A closed requisition cannot be edited.",
      );
    });
    expect(mockRefresh).not.toHaveBeenCalled();
  });
});
