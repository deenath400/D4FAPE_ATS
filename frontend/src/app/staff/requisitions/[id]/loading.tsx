import React from "react";

export default function Loading() {
  return (
    <div className="space-y-8 max-w-2xl animate-pulse">
      <div className="flex items-center justify-between">
        <div className="h-8 w-64 bg-slate-800 rounded"></div>
        <div className="h-4 w-16 bg-slate-800 rounded"></div>
      </div>
      <div className="h-9 w-40 bg-slate-800 rounded-lg"></div>
      <div className="h-96 bg-slate-800/50 rounded-2xl"></div>
    </div>
  );
}
