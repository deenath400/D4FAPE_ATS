"use client";

import React, { useState, useEffect } from "react";
import { SystemStatusDto } from "@/lib/types/system-status";
import { StatusSkeleton } from "@/components/StatusSkeleton";

export function ClientStatusPanel() {
  const [status, setStatus] = useState<SystemStatusDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState<boolean>(true);

  useEffect(() => {
    let isMounted = true;
    async function fetchStatus() {
      try {
        const res = await fetch("/api/bff/system-status");
        if (!res.ok) {
          if (isMounted) setError("Unable to reach the backend service.");
          return;
        }
        const data = (await res.json()) as SystemStatusDto;
        if (isMounted) {
          setStatus(data);
        }
      } catch {
        if (isMounted) {
          setError("Unable to reach the backend service.");
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    fetchStatus();
    return () => {
      isMounted = false;
    };
  }, []);

  if (loading) {
    return <StatusSkeleton label="Browser-retrieved" />;
  }

  return (
    <div
      data-testid="client-status-panel"
      className="p-6 rounded-lg border border-slate-700 bg-slate-800 shadow-md"
    >
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-semibold text-slate-200">Browser-retrieved Status</h3>
        <span className="px-2.5 py-0.5 rounded text-xs font-medium bg-emerald-900/50 text-emerald-300 border border-emerald-700">
          Browser-retrieved
        </span>
      </div>

      {error ? (
        <div className="p-4 rounded bg-red-950/40 border border-red-800 text-red-300">
          <p className="font-medium">{error}</p>
        </div>
      ) : status ? (
        <div className="space-y-3">
          <div className="flex justify-between items-center text-sm">
            <span className="text-slate-400">Backend Version:</span>
            <span className="font-mono text-slate-200">{status.version}</span>
          </div>
          <div className="flex justify-between items-center text-sm">
            <span className="text-slate-400">Database Reachable:</span>
            <span
              className={`font-semibold ${
                status.database.reachable ? "text-emerald-400" : "text-red-400"
              }`}
            >
              {status.database.reachable ? "Yes" : "No"}
            </span>
          </div>
          <div className="flex justify-between items-center text-sm">
            <span className="text-slate-400">Schema Current:</span>
            <span
              className={`font-semibold ${
                status.database.schemaCurrent ? "text-emerald-400" : "text-red-400"
              }`}
            >
              {status.database.schemaCurrent ? "Yes" : "No"}
            </span>
          </div>
        </div>
      ) : null}
    </div>
  );
}
