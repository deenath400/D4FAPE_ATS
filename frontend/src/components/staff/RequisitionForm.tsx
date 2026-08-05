"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import type { RequisitionDto } from "@/lib/types/requisition";

const MAX_TITLE_LENGTH = 200;

export type RequisitionFormProps = {
  mode: "create" | "edit";
  requisitionId?: string;
  initialTitle?: string;
  initialDescription?: string;
};

function validate(title: string, description: string): string | null {
  if (!title.trim()) {
    return "Title is required.";
  }
  if (title.length > MAX_TITLE_LENGTH) {
    return "Title must be 200 characters or fewer.";
  }
  if (!description.trim()) {
    return "Description is required.";
  }
  return null;
}

// Create/edit form (LLD §5.1). Mirrors `RegisterForm.tsx`'s structure: local state, inline
// `role="alert"` error banner, submit-button spinner. Mutates via the `ui/bff` proxy, never
// the backend directly (`coding-standards.md`).
export function RequisitionForm({
  mode,
  requisitionId,
  initialTitle = "",
  initialDescription = "",
}: RequisitionFormProps) {
  const router = useRouter();
  const [title, setTitle] = useState(initialTitle);
  const [description, setDescription] = useState(initialDescription);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const validationError = validate(title, description);
    if (validationError) {
      setError(validationError);
      return;
    }

    setLoading(true);

    try {
      const path =
        mode === "create"
          ? "/api/bff/proxy/requisitions"
          : `/api/bff/proxy/requisitions/${requisitionId}`;
      const method = mode === "create" ? "POST" : "PUT";

      const res = await fetch(path, {
        method,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ title, description }),
      });

      if (!res.ok) {
        const problem = await res.json().catch(() => null);
        setError(
          problem?.detail ||
            problem?.title ||
            "Unable to save the requisition. Please try again.",
        );
        setLoading(false);
        return;
      }

      if (mode === "create") {
        const created = (await res.json()) as RequisitionDto;
        router.push(`/staff/requisitions/${created.id}`);
        return;
      }

      router.refresh();
      setLoading(false);
    } catch {
      setError("An unexpected network error occurred. Please try again.");
      setLoading(false);
    }
  };

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
            htmlFor="requisition-title"
            className="block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-1"
          >
            Title
          </label>
          <input
            id="requisition-title"
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            disabled={loading}
            className="w-full px-3 py-2 bg-slate-900/90 border border-slate-700 rounded-lg text-slate-100 placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent transition-all text-sm disabled:opacity-50"
            placeholder="Senior Backend Engineer"
          />
        </div>

        <div>
          <label
            htmlFor="requisition-description"
            className="block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-1"
          >
            Description
          </label>
          <textarea
            id="requisition-description"
            rows={8}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            disabled={loading}
            className="w-full px-3 py-2 bg-slate-900/90 border border-slate-700 rounded-lg text-slate-100 placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent transition-all text-sm disabled:opacity-50"
            placeholder="We are looking for..."
          />
        </div>

        <button
          type="submit"
          disabled={loading}
          className="w-full mt-6 py-2.5 px-4 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-lg shadow-lg hover:shadow-emerald-500/20 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2 focus:ring-offset-slate-900 transition-all text-sm disabled:opacity-50"
        >
          {loading
            ? mode === "create"
              ? "Creating..."
              : "Saving..."
            : mode === "create"
              ? "Create Requisition"
              : "Save Changes"}
        </button>
      </form>
    </div>
  );
}
