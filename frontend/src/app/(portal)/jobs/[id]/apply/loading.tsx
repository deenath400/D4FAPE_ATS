import React from "react";

export default function Loading() {
  return (
    <main className="min-h-screen bg-slate-900 text-slate-100 p-6 md:p-12">
      <div className="max-w-2xl mx-auto space-y-8 animate-pulse">
        <div className="h-6 w-32 bg-slate-800 rounded"></div>
        <div className="h-8 w-2/3 bg-slate-800 rounded"></div>
        <div className="h-4 w-1/2 bg-slate-800 rounded"></div>
        <div className="h-64 bg-slate-800/50 rounded-2xl"></div>
      </div>
    </main>
  );
}
