import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { ApplicationForm } from "../../src/components/portal/ApplicationForm";

function selectFile(input: HTMLElement, file: File) {
  fireEvent.change(input, { target: { files: [file] } });
}

describe("ApplicationForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows a validation error banner for a non-PDF file (AC-3)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({
        title: "Validation",
        code: "application.submit.invalid-file-type",
        errors: { cv: ["Only PDF files are accepted."] },
      }),
    } as Response);

    render(<ApplicationForm requisitionId="req-1" requisitionTitle="Senior Backend Engineer" />);

    const file = new File(["not a pdf"], "resume.docx", {
      type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    });
    selectFile(screen.getByLabelText(/cv \(pdf/i), file);
    fireEvent.click(screen.getByRole("button", { name: /submit application/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent("Only PDF files are accepted.");
    });
  });

  it("shows a duplicate-submission error banner on 409 (AC-8)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({
        detail: "You have already applied to this requisition.",
        code: "application.submit.duplicate",
      }),
    } as Response);

    render(<ApplicationForm requisitionId="req-1" requisitionTitle="Senior Backend Engineer" />);

    const file = new File(["%PDF-1.4"], "resume.pdf", { type: "application/pdf" });
    selectFile(screen.getByLabelText(/cv \(pdf/i), file);
    fireEvent.click(screen.getByRole("button", { name: /submit application/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent("You've already applied to this role.");
    });
  });

  it("shows a success panel after a 201 (AC-1)", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 201,
      json: async () => ({
        id: "app-1",
        requisitionId: "req-1",
        candidateId: "candidate-1",
        submittedAtUtc: "2026-08-06T10:15:00Z",
        cv: { fileName: "resume.pdf", contentType: "application/pdf", sizeBytes: 1024 },
      }),
    } as Response);

    render(<ApplicationForm requisitionId="req-1" requisitionTitle="Senior Backend Engineer" />);

    const file = new File(["%PDF-1.4"], "resume.pdf", { type: "application/pdf" });
    selectFile(screen.getByLabelText(/cv \(pdf/i), file);
    fireEvent.click(screen.getByRole("button", { name: /submit application/i }));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/bff/proxy/requisitions/req-1/applications",
        expect.objectContaining({ method: "POST" }),
      );
      expect(screen.getByRole("status")).toHaveTextContent("Application submitted");
      expect(
        screen.getByRole("link", { name: /view my applications/i }),
      ).toHaveAttribute("href", "/applications");
    });
  });

  it("shows a client-side error and does not submit when no file is selected (AC-2)", async () => {
    global.fetch = vi.fn();

    render(<ApplicationForm requisitionId="req-1" requisitionTitle="Senior Backend Engineer" />);
    fireEvent.click(screen.getByRole("button", { name: /submit application/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent("A CV file is required.");
    });
    expect(global.fetch).not.toHaveBeenCalled();
  });
});
