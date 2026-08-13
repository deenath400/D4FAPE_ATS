import React from "react";

export default function Loading() {
  return (
    <div className="space-y-6 max-w-2xl animate-pulse">
      <div className="flex items-center justify-between">
        <div className="h-8 w-56 bg-slate-800 rounded"></div>
        <div className="h-4 w-32 bg-slate-800 rounded"></div>
      </div>
      <div className="space-y-3">
        {[0, 1, 2].map((row) => (
          <div key={row} className="h-20 bg-slate-800/50 rounded-lg border border-slate-800"></div>
        ))}
      </div>
    </div>
  );
}
