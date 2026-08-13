"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import type { StageDto } from "@/lib/types/pipeline";

const MAX_NAME_LENGTH = 200;

export type StageConfigPanelProps = {
  requisitionId: string;
  stages: StageDto[];
  canWrite: boolean;
};

function validateName(name: string): string | null {
  if (!name.trim()) {
    return "Stage name is required.";
  }
  if (name.length > MAX_NAME_LENGTH) {
    return "Stage name must be 200 characters or fewer.";
  }
  return null;
}

async function readProblemMessage(res: Response, fallback: string): Promise<string> {
  const problem = await res.json().catch(() => null);
  return problem?.detail || problem?.title || fallback;
}

// Add/rename/reorder/remove a Requisition's Stages (LLD §5.1, AC-1, AC-3, AC-4, AC-5, AC-6,
// AC-31). `canWrite=false` (HiringManager) hides every write affordance and renders a read-only
// list, mirroring `RequisitionLifecycleActions`. No drag-and-drop library is in
// `tech-stack.md` — reordering is up/down buttons that submit the full new order.
export function StageConfigPanel({
  requisitionId,
  stages: initialStages,
  canWrite,
}: StageConfigPanelProps) {
  const router = useRouter();
  const [stages, setStages] = useState<StageDto[]>(initialStages);
  const [newName, setNewName] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setStages(initialStages);
  }, [initialStages]);

  const ordered = [...stages].sort((a, b) => a.sortOrder - b.sortOrder);

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const validationError = validateName(newName);
    if (validationError) {
      setError(validationError);
      return;
    }

    setLoading(true);
    try {
      const res = await fetch(`/api/bff/proxy/requisitions/${requisitionId}/stages`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: newName }),
      });

      if (!res.ok) {
        setError(await readProblemMessage(res, "Unable to add the stage. Please try again."));
        setLoading(false);
        return;
      }

      setNewName("");
      router.refresh();
      setLoading(false);
    } catch {
      setError("An unexpected network error occurred. Please try again.");
      setLoading(false);
    }
  };

  const startRename = (stage: StageDto) => {
    setError(null);
    setEditingId(stage.id);
    setEditingName(stage.name);
  };

  const cancelRename = () => {
    setEditingId(null);
    setEditingName("");
  };

  const handleRename = async (stageId: string) => {
    setError(null);

    const validationError = validateName(editingName);
    if (validationError) {
      setError(validationError);
      return;
    }

    setLoading(true);
    try {
      const res = await fetch(`/api/bff/proxy/requisitions/${requisitionId}/stages/${stageId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: editingName }),
      });

      if (!res.ok) {
        setError(await readProblemMessage(res, "Unable to rename the stage. Please try again."));
        setLoading(false);
        return;
      }

      setEditingId(null);
      setEditingName("");
      router.refresh();
      setLoading(false);
    } catch {
      setError("An unexpected network error occurred. Please try again.");
      setLoading(false);
    }
  };

  const handleRemove = async (stageId: string) => {
    setError(null);
    setLoading(true);
    try {
      const res = await fetch(`/api/bff/proxy/requisitions/${requisitionId}/stages/${stageId}`, {
        method: "DELETE",
      });

      if (!res.ok) {
        setError(await readProblemMessage(res, "Unable to remove the stage. Please try again."));
        setLoading(false);
        return;
      }

      router.refresh();
      setLoading(false);
    } catch {
      setError("An unexpected network error occurred. Please try again.");
      setLoading(false);
    }
  };

  const handleMove = async (stageId: string, direction: -1 | 1) => {
    setError(null);
    const index = ordered.findIndex((s) => s.id === stageId);
    const targetIndex = index + direction;
    if (index < 0 || targetIndex < 0 || targetIndex >= ordered.length) {
      return;
    }

    const reordered = [...ordered];
    [reordered[index], reordered[targetIndex]] = [reordered[targetIndex], reordered[index]];
    setStages(reordered.map((s, i) => ({ ...s, sortOrder: i })));

    setLoading(true);
    try {
      const res = await fetch(`/api/bff/proxy/requisitions/${requisitionId}/stages/reorder`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ stageIds: reordered.map((s) => s.id) }),
      });

      if (!res.ok) {
        setError(await readProblemMessage(res, "Unable to reorder stages. Please try again."));
        setStages(initialStages);
        setLoading(false);
        return;
      }

      router.refresh();
      setLoading(false);
    } catch {
      setError("An unexpected network error occurred. Please try again.");
      setStages(initialStages);
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      {error && (
        <div
          role="alert"
          className="p-4 bg-red-950/70 border border-red-800/80 rounded-xl text-red-200 text-sm leading-relaxed"
        >
          {error}
        </div>
      )}

      {ordered.length === 0 ? (
        <div className="p-6 rounded-lg border border-slate-700 bg-slate-800/50 text-center text-slate-400">
          <p>No stages configured — add one to accept applications.</p>
        </div>
      ) : (
        <ul className="divide-y divide-slate-800 rounded-lg border border-slate-700 overflow-hidden">
          {ordered.map((stage, index) => (
            <li
              key={stage.id}
              className="flex items-center justify-between gap-3 px-4 py-3 bg-slate-900"
            >
              {editingId === stage.id ? (
                <div className="flex flex-1 items-center gap-2">
                  <label htmlFor={`stage-name-${stage.id}`} className="sr-only">
                    Stage name
                  </label>
                  <input
                    id={`stage-name-${stage.id}`}
                    type="text"
                    value={editingName}
                    onChange={(e) => setEditingName(e.target.value)}
                    disabled={loading}
                    className="flex-1 px-3 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 text-sm disabled:opacity-50"
                  />
                  <button
                    type="button"
                    disabled={loading}
                    onClick={() => handleRename(stage.id)}
                    className="px-3 py-1.5 text-sm font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg disabled:opacity-50"
                  >
                    Save
                  </button>
                  <button
                    type="button"
                    disabled={loading}
                    onClick={cancelRename}
                    className="px-3 py-1.5 text-sm font-medium bg-slate-700 hover:bg-slate-600 text-white rounded-lg disabled:opacity-50"
                  >
                    Cancel
                  </button>
                </div>
              ) : (
                <span className="text-slate-100 font-medium">{stage.name}</span>
              )}

              {canWrite && editingId !== stage.id && (
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    aria-label={`Move ${stage.name} up`}
                    disabled={loading || index === 0}
                    onClick={() => handleMove(stage.id, -1)}
                    className="px-2 py-1 text-sm bg-slate-700 hover:bg-slate-600 text-white rounded disabled:opacity-30"
                  >
                    &uarr;
                  </button>
                  <button
                    type="button"
                    aria-label={`Move ${stage.name} down`}
                    disabled={loading || index === ordered.length - 1}
                    onClick={() => handleMove(stage.id, 1)}
                    className="px-2 py-1 text-sm bg-slate-700 hover:bg-slate-600 text-white rounded disabled:opacity-30"
                  >
                    &darr;
                  </button>
                  <button
                    type="button"
                    disabled={loading}
                    onClick={() => startRename(stage)}
                    className="px-3 py-1.5 text-sm font-medium bg-slate-700 hover:bg-slate-600 text-white rounded-lg disabled:opacity-50"
                  >
                    Rename
                  </button>
                  <button
                    type="button"
                    disabled={loading}
                    onClick={() => handleRemove(stage.id)}
                    className="px-3 py-1.5 text-sm font-medium bg-red-800 hover:bg-red-700 text-white rounded-lg disabled:opacity-50"
                  >
                    Remove
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {canWrite && (
        <form onSubmit={handleAdd} className="flex items-end gap-2 pt-2">
          <div className="flex-1">
            <label
              htmlFor="new-stage-name"
              className="block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-1"
            >
              New stage name
            </label>
            <input
              id="new-stage-name"
              type="text"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              disabled={loading}
              className="w-full px-3 py-2 bg-slate-900/90 border border-slate-700 rounded-lg text-slate-100 placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent transition-all text-sm disabled:opacity-50"
              placeholder="Final Interview"
            />
          </div>
          <button
            type="submit"
            disabled={loading}
            className="px-3.5 py-2 font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg shadow-md transition-all text-sm disabled:opacity-50"
          >
            {loading ? "Adding..." : "Add Stage"}
          </button>
        </form>
      )}
    </div>
  );
}
