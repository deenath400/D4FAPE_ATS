import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { RequisitionLifecycleActions } from "../../src/components/staff/RequisitionLifecycleActions";

const mockRefresh = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    refresh: mockRefresh,
  }),
}));

describe("RequisitionLifecycleActions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders nothing when canWrite is false (HiringManager)", () => {
    const { container } = render(
      <RequisitionLifecycleActions requisitionId="req-1" status="draft" canWrite={false} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("renders only Publish for a draft requisition (AC-6)", () => {
    render(<RequisitionLifecycleActions requisitionId="req-1" status="draft" canWrite={true} />);
    expect(screen.getByRole("button", { name: /publish/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /unpublish/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /close/i })).not.toBeInTheDocument();
  });

  it("renders Unpublish and Close for a published requisition (AC-7, AC-9)", () => {
    render(
      <RequisitionLifecycleActions requisitionId="req-1" status="published" canWrite={true} />,
    );
    expect(screen.getByRole("button", { name: /unpublish/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /close/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^publish$/i })).not.toBeInTheDocument();
  });

  it("calls the publish proxy endpoint and refreshes on success", async () => {
    global.fetch = vi
      .fn()
      .mockResolvedValue({ ok: true, status: 200, json: async () => ({}) } as Response);

    render(<RequisitionLifecycleActions requisitionId="req-1" status="draft" canWrite={true} />);
    fireEvent.click(screen.getByRole("button", { name: /publish/i }));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/bff/proxy/requisitions/req-1/publish",
        expect.objectContaining({ method: "POST" }),
      );
      expect(mockRefresh).toHaveBeenCalled();
    });
  });

  it("shows a 409 error banner on an invalid transition (AC-10, AC-11)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({
        detail: "Only a draft requisition can be published.",
        code: "requisition.publish.invalid-transition",
      }),
    } as Response);

    render(<RequisitionLifecycleActions requisitionId="req-1" status="draft" canWrite={true} />);
    fireEvent.click(screen.getByRole("button", { name: /publish/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(
        "Only a draft requisition can be published.",
      );
    });
    expect(mockRefresh).not.toHaveBeenCalled();
  });
});
