"use client";

import React from "react";

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="p-6 rounded-lg border border-red-800/80 bg-red-950/40 text-red-200 max-w-2xl">
      <h2 className="text-lg font-semibold mb-2">Unable to load this requisition</h2>
      <p className="text-sm mb-4">{error.message || "An unexpected error occurred."}</p>
      <button
        type="button"
        onClick={() => reset()}
        className="px-3.5 py-1.5 font-medium bg-red-800 hover:bg-red-700 text-white rounded-lg transition-all text-sm"
      >
        Try again
      </button>
    </div>
  );
}
