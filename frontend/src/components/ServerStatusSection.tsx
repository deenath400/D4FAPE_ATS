import React from "react";
import { invokeBackend } from "@/lib/server/backend-invoke";
import { SystemStatusDto } from "@/lib/types/system-status";

export async function ServerStatusSection() {
  let status: SystemStatusDto | null = null;
  let errorMsg: string | null = null;

  try {
    status = await invokeBackend<SystemStatusDto>({ path: "/api/system/status" });
  } catch {
    errorMsg = "Unable to reach the backend service.";
  }

  return (
    <div
      data-testid="server-status-section"
      className="p-6 rounded-lg border border-slate-700 bg-slate-800 shadow-md"
    >
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-semibold text-slate-200">Server-rendered Status</h3>
        <span className="px-2.5 py-0.5 rounded text-xs font-medium bg-blue-900/50 text-blue-300 border border-blue-700">
          Server-rendered
        </span>
      </div>

      {errorMsg ? (
        <div className="p-4 rounded bg-red-950/40 border border-red-800 text-red-300">
          <p className="font-medium">{errorMsg}</p>
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
