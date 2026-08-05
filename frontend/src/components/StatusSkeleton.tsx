import React from "react";

export type StatusSkeletonProps = {
  label: string;
};

export function StatusSkeleton({ label }: StatusSkeletonProps) {
  return (
    <div
      data-testid="status-skeleton"
      className="p-6 rounded-lg border border-slate-700 bg-slate-800/50 shadow-md animate-pulse"
    >
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-semibold text-slate-300">{label}</h3>
        <span className="px-2.5 py-0.5 rounded text-xs font-medium bg-slate-700 text-slate-400">
          Loading...
        </span>
      </div>
      <div className="space-y-3">
        <div className="h-4 bg-slate-700 rounded w-1/3"></div>
        <div className="h-4 bg-slate-700 rounded w-2/3"></div>
      </div>
    </div>
  );
}
