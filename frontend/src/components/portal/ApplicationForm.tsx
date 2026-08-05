"use client";

import React, { useState } from "react";
import Link from "next/link";

export type ApplicationFormProps = {
  requisitionId: string;
  requisitionTitle: string;
};

type SubmitProblem = {
  code?: string;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};

// Maps `application.submit.*` ProblemDetails to candidate-facing copy (LLD §5.3). Field-level
// messages (invalid type, oversized, missing) come straight from the backend's `errors.cv`
// array — the backend, not this component, owns the wording for those.
function describeSubmitError(problem: SubmitProblem | null): string {
  if (problem?.code === "application.submit.duplicate") {
    return "You've already applied to this role.";
  }

  const cvErrors = problem?.errors?.cv;
  if (cvErrors && cvErrors.length > 0) {
    return cvErrors[0];
  }

  return (
    problem?.detail || problem?.title || "Unable to submit your application. Please try again."
  );
}

// Client Component: CV file input + submit (LLD §5.1). Mirrors `RegisterForm.tsx`'s
// error/loading structure. Never sets `Content-Type` manually — the browser sets the
// multipart boundary for the `FormData` body.
export function ApplicationForm({ requisitionId, requisitionTitle }: ApplicationFormProps) {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!file) {
      setError("A CV file is required.");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const formData = new FormData();
      formData.append("cv", file);

      const res = await fetch(`/api/bff/proxy/requisitions/${requisitionId}/applications`, {
        method: "POST",
        body: formData,
      });

      if (!res.ok) {
        const problem = (await res.json().catch(() => null)) as SubmitProblem | null;
        setError(describeSubmitError(problem));
        setLoading(false);
        return;
      }

      setSubmitted(true);
      setLoading(false);
    } catch {
      setError("An unexpected network error occurred. Please try again.");
      setLoading(false);
    }
  };

  if (submitted) {
    return (
      <div
        role="status"
        className="p-6 rounded-2xl border border-emerald-800/80 bg-emerald-950/40 text-emerald-200 space-y-3"
      >
        <h3 className="text-lg font-semibold text-emerald-100">Application submitted</h3>
        <p className="text-sm">
          Your application for &ldquo;{requisitionTitle}&rdquo; has been received.
        </p>
        <Link
          href="/applications"
          className="inline-block text-sm font-medium text-emerald-300 hover:text-emerald-200"
        >
          View my applications &rarr;
        </Link>
      </div>
    );
  }

  return (
    <div className="w-full bg-slate-800/80 border border-slate-700/80 rounded-2xl p-8 shadow-2xl">
      {error && (
        <div
          role="alert"
          className="mb-6 p-4 bg-red-950/70 border border-red-800/80 rounded-xl text-red-200 text-sm leading-relaxed"
        >
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label
            htmlFor="application-cv"
            className="block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-1"
          >
            CV (PDF, max 5 MB)
          </label>
          <input
            id="application-cv"
            type="file"
            accept="application/pdf,.pdf"
            disabled={loading}
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            className="w-full text-sm text-slate-300 file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border-0 file:bg-emerald-600 file:text-white file:font-medium hover:file:bg-emerald-500 disabled:opacity-50"
          />
        </div>

        <button
          type="submit"
          disabled={loading}
          className="w-full mt-6 py-2.5 px-4 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-lg shadow-lg hover:shadow-emerald-500/20 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2 focus:ring-offset-slate-900 transition-all text-sm disabled:opacity-50"
        >
          {loading ? "Submitting..." : "Submit Application"}
        </button>
      </form>
    </div>
  );
}
