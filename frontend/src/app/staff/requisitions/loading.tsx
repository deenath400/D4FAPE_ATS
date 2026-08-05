import React from "react";

export default function Loading() {
  return (
    <div className="space-y-6 animate-pulse">
      <div className="flex items-center justify-between">
        <div className="h-8 w-48 bg-slate-800 rounded"></div>
        <div className="h-9 w-36 bg-slate-800 rounded-lg"></div>
      </div>
      <div className="rounded-lg border border-slate-700 overflow-hidden">
        {[0, 1, 2, 3, 4].map((row) => (
          <div key={row} className="h-12 bg-slate-800/50 border-b border-slate-800 last:border-0"></div>
        ))}
      </div>
    </div>
  );
}
