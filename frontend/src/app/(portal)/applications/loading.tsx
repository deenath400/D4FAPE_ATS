import React from "react";

export default function Loading() {
  return (
    <main className="min-h-screen bg-slate-900 text-slate-100 p-6 md:p-12">
      <div className="max-w-3xl mx-auto space-y-8 animate-pulse">
        <div className="h-10 w-64 bg-slate-800 rounded"></div>
        <div className="space-y-4">
          {[0, 1, 2].map((row) => (
            <div key={row} className="h-20 bg-slate-800/50 rounded-lg"></div>
          ))}
        </div>
      </div>
    </main>
  );
}
