import React from "react";

export default function Loading() {
  return (
    <div className="space-y-6 animate-pulse">
      <div className="flex items-center justify-between">
        <div className="h-8 w-48 bg-slate-800 rounded"></div>
        <div className="h-4 w-32 bg-slate-800 rounded"></div>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        {[0, 1, 2, 3].map((col) => (
          <div key={col} className="h-64 bg-slate-800/50 rounded-lg border border-slate-800"></div>
        ))}
      </div>
    </div>
  );
}
