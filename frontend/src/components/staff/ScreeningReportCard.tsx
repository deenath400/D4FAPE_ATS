"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import type { ScreeningReportDto } from "@/lib/types/screening";
import { ScreeningBadge } from "@/components/staff/ScreeningBadge";

export type ScreeningReportCardProps = {
  applicationId: string;
  report: ScreeningReportDto | null;
  canReScreen?: boolean;
};

export function ScreeningReportCard({
  applicationId,
  report: initialReport,
  canReScreen = false,
}: ScreeningReportCardProps) {
  const router = useRouter();
  const [report, setReport] = useState<ScreeningReportDto | null>(initialReport);
  const [reScreening, setReScreening] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleReScreen = async () => {
    setReScreening(true);
    setError(null);
    try {
      const res = await fetch(`/api/bff/proxy/api/staff/applications/${applicationId}/screen`, {
        method: "POST",
      });
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.detail || body?.title || `Re-screen failed (${res.status})`);
      }
      const updated = (await res.json()) as ScreeningReportDto;
      setReport(updated);
      router.refresh();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to re-screen application");
    } finally {
      setReScreening(false);
    }
  };

  if (!report) {
    return (
      <div className="p-5 rounded-lg border border-slate-700 bg-slate-900/60 space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="font-semibold text-slate-100">AI Screening</h3>
          <span className="text-xs text-slate-500">Not screened</span>
        </div>
        <p className="text-xs text-slate-400">No screening report has been generated for this application.</p>
        {canReScreen && (
          <button
            type="button"
            disabled={reScreening}
            onClick={handleReScreen}
            className="px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-medium transition-colors disabled:opacity-50"
          >
            {reScreening ? "Screening..." : "Run AI Screening"}
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="p-5 rounded-lg border border-slate-700 bg-slate-900 space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-slate-800 pb-3">
        <div className="flex items-center space-x-2">
          <h3 className="font-semibold text-slate-100">AI Screening Analysis</h3>
          <ScreeningBadge
            score={report.score}
            recommendation={report.recommendation}
            status={report.status}
          />
        </div>
        <div className="text-right">
          <span className="text-2xl font-bold text-slate-100">{report.score}</span>
          <span className="text-xs text-slate-400 font-medium ml-1">/ 100</span>
        </div>
      </div>

      {error && (
        <div className="p-3 rounded bg-rose-950/40 border border-rose-800/60 text-rose-300 text-xs">
          {error}
        </div>
      )}

      {/* Failure reason if failed */}
      {report.status === "Failed" && report.failureReason && (
        <div className="p-3 rounded bg-rose-950/30 border border-rose-900/50 text-xs text-rose-400">
          <span className="font-bold">Failure: </span>
          {report.failureReason}
        </div>
      )}

      {/* Summary */}
      {report.summary && (
        <div className="space-y-1">
          <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Executive Summary</p>
          <p className="text-xs text-slate-200 leading-relaxed bg-slate-800/40 p-2.5 rounded border border-slate-800">
            {report.summary}
          </p>
        </div>
      )}

      {/* Strengths and Concerns */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <div className="p-3 rounded bg-emerald-950/20 border border-emerald-900/40 space-y-1.5">
          <p className="text-xs font-bold text-emerald-300 uppercase tracking-wider">
            ✓ Strengths ({report.strengths?.length || 0})
          </p>
          {report.strengths && report.strengths.length > 0 ? (
            <ul className="space-y-1 text-xs text-slate-300 list-disc list-inside">
              {report.strengths.map((s, idx) => (
                <li key={idx}>{s}</li>
              ))}
            </ul>
          ) : (
            <p className="text-xs text-slate-500 italic">None noted</p>
          )}
        </div>

        <div className="p-3 rounded bg-amber-950/20 border border-amber-900/40 space-y-1.5">
          <p className="text-xs font-bold text-amber-300 uppercase tracking-wider">
            ! Areas for Review ({report.concerns?.length || 0})
          </p>
          {report.concerns && report.concerns.length > 0 ? (
            <ul className="space-y-1 text-xs text-slate-300 list-disc list-inside">
              {report.concerns.map((c, idx) => (
                <li key={idx}>{c}</li>
              ))}
            </ul>
          ) : (
            <p className="text-xs text-slate-500 italic">None noted</p>
          )}
        </div>
      </div>

      {/* Footer / Re-screen */}
      <div className="pt-2 flex items-center justify-between text-xs text-slate-500">
        <span>Evaluated {new Date(report.screenedAtUtc).toLocaleString()}</span>
        {canReScreen && (
          <button
            type="button"
            disabled={reScreening}
            onClick={handleReScreen}
            className="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium border border-slate-700 transition-colors disabled:opacity-50"
          >
            {reScreening ? "Re-evaluating..." : "↻ Re-screen"}
          </button>
        )}
      </div>
    </div>
  );
}
