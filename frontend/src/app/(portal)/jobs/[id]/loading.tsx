import React from "react";

export default function Loading() {
  return (
    <main className="min-h-screen bg-slate-900 text-slate-100 p-6 md:p-12">
      <div className="max-w-3xl mx-auto space-y-8 animate-pulse">
        <div className="h-6 w-32 bg-slate-800 rounded"></div>
        <div className="h-10 w-3/4 bg-slate-800 rounded"></div>
        <div className="h-4 w-40 bg-slate-800 rounded"></div>
        <div className="space-y-2">
          <div className="h-4 bg-slate-800/70 rounded"></div>
          <div className="h-4 bg-slate-800/70 rounded"></div>
          <div className="h-4 w-2/3 bg-slate-800/70 rounded"></div>
        </div>
      </div>
    </main>
  );
}
