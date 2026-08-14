"use client";

import React, { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import type { ScreeningReportDto } from "@/lib/types/screening";
import { ScreeningBadge } from "@/components/staff/ScreeningBadge";

export type ScreeningReportModalProps = {
  applicationId: string;
  candidateName: string;
  isOpen: boolean;
  onClose: () => void;
  canReScreen?: boolean;
};

export function ScreeningReportModal({
  applicationId,
  candidateName,
  isOpen,
  onClose,
  canReScreen = false,
}: ScreeningReportModalProps) {
  const router = useRouter();
  const [report, setReport] = useState<ScreeningReportDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [reScreening, setReScreening] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen || !applicationId) return;

    let ignore = false;
    setLoading(true);
    setError(null);

    fetch(`/api/bff/proxy/api/staff/applications/${applicationId}/screening-report`)
      .then(async (res) => {
        if (!res.ok) {
          const body = await res.json().catch(() => null);
          throw new Error(body?.detail || body?.title || `Failed with status ${res.status}`);
        }
        return res.json() as Promise<ScreeningReportDto>;
      })
      .then((data) => {
        if (!ignore) {
          setReport(data);
          setLoading(false);
        }
      })
      .catch((err) => {
        if (!ignore) {
          setError(err.message);
          setLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [isOpen, applicationId]);

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

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm animate-fade-in">
      <div className="relative w-full max-w-2xl rounded-xl border border-slate-700 bg-slate-900 shadow-2xl p-6 space-y-5 max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-start justify-between border-b border-slate-800 pb-4">
          <div>
            <h2 className="text-xl font-bold text-slate-100">AI Screening Report</h2>
            <p className="text-sm text-slate-400">Applicant: {candidateName}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-200 p-1.5 rounded-lg hover:bg-slate-800 transition-colors"
          >
            <span className="sr-only">Close</span>
            ✕
          </button>
        </div>

        {/* Loading state */}
        {loading && (
          <div className="py-12 text-center text-slate-400 space-y-3">
            <div className="inline-block w-8 h-8 border-2 border-emerald-500 border-t-transparent rounded-full animate-spin" />
            <p className="text-sm">Loading screening analysis...</p>
          </div>
        )}

        {/* Error state */}
        {!loading && error && (
          <div className="p-4 rounded-lg bg-rose-950/40 border border-rose-800/60 text-rose-300 text-sm">
            <p className="font-semibold">Unable to load report</p>
            <p className="text-rose-400/90 text-xs mt-1">{error}</p>
          </div>
        )}

        {/* Report Content */}
        {!loading && report && (
          <div className="space-y-6">
            {/* Score and Recommendation Banner */}
            <div className="flex items-center justify-between p-4 rounded-lg bg-slate-800/60 border border-slate-700">
              <div className="space-y-1">
                <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Evaluation Status</p>
                <div className="flex items-center space-x-2">
                  <ScreeningBadge
                    score={report.score}
                    recommendation={report.recommendation}
                    status={report.status}
                  />
                  <span className="text-xs text-slate-500">
                    Screened {new Date(report.evaluatedAtUtc || report.screenedAtUtc || "").toLocaleString()}
                  </span>
                </div>
              </div>
              <div className="text-right">
                <span className="text-3xl font-extrabold text-slate-100">{report.score}</span>
                <span className="text-xs text-slate-400 font-medium ml-1">/ 100</span>
              </div>
            </div>

            {/* Failure notice if failed */}
            {report.status === "Failed" && report.failureReason && (
              <div className="p-4 rounded-lg bg-rose-950/30 border border-rose-900/50 space-y-1">
                <h4 className="text-xs font-bold text-rose-300 uppercase tracking-wider">Failure Details</h4>
                <p className="text-sm text-rose-400">{report.failureReason}</p>
              </div>
            )}

            {/* Category Breakdown Scores (0009) */}
            {(report.skillsScore != null || report.experienceScore != null || report.educationScore != null) && (
              <div className="space-y-2 p-4 rounded-lg bg-slate-800/40 border border-slate-700/80">
                <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider">Evaluation Breakdown</h4>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  {report.skillsScore != null && (
                    <div className="p-2.5 rounded bg-slate-900/60 border border-slate-800 space-y-1.5">
                      <div className="flex justify-between text-xs">
                        <span className="text-slate-400">Skills Fit</span>
                        <span className="font-semibold text-slate-200">{report.skillsScore}%</span>
                      </div>
                      <div className="w-full bg-slate-700/60 rounded-full h-2 overflow-hidden">
                        <div
                          className="bg-indigo-500 h-2 rounded-full transition-all"
                          style={{ width: `${Math.min(100, Math.max(0, report.skillsScore))}%` }}
                        />
                      </div>
                    </div>
                  )}
                  {report.experienceScore != null && (
                    <div className="p-2.5 rounded bg-slate-900/60 border border-slate-800 space-y-1.5">
                      <div className="flex justify-between text-xs">
                        <span className="text-slate-400">Experience Fit</span>
                        <span className="font-semibold text-slate-200">{report.experienceScore}%</span>
                      </div>
                      <div className="w-full bg-slate-700/60 rounded-full h-2 overflow-hidden">
                        <div
                          className="bg-emerald-500 h-2 rounded-full transition-all"
                          style={{ width: `${Math.min(100, Math.max(0, report.experienceScore))}%` }}
                        />
                      </div>
                    </div>
                  )}
                  {report.educationScore != null && (
                    <div className="p-2.5 rounded bg-slate-900/60 border border-slate-800 space-y-1.5">
                      <div className="flex justify-between text-xs">
                        <span className="text-slate-400">Education Fit</span>
                        <span className="font-semibold text-slate-200">{report.educationScore}%</span>
                      </div>
                      <div className="w-full bg-slate-700/60 rounded-full h-2 overflow-hidden">
                        <div
                          className="bg-amber-500 h-2 rounded-full transition-all"
                          style={{ width: `${Math.min(100, Math.max(0, report.educationScore))}%` }}
                        />
                      </div>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Summary */}
            {report.summary && (
              <div className="space-y-1.5">
                <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider">Executive Summary</h4>
                <p className="text-sm text-slate-200 leading-relaxed bg-slate-800/30 p-3 rounded border border-slate-800">
                  {report.summary}
                </p>
              </div>
            )}

            {/* Strengths & Concerns Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Strengths */}
              <div className="p-4 rounded-lg bg-emerald-950/20 border border-emerald-900/40 space-y-2">
                <h4 className="text-xs font-bold text-emerald-300 uppercase tracking-wider flex items-center">
                  <span className="mr-1.5">✓</span> Key Strengths ({report.strengths?.length || 0})
                </h4>
                {report.strengths && report.strengths.length > 0 ? (
                  <ul className="space-y-1.5 text-xs text-slate-300 list-disc list-inside">
                    {report.strengths.map((s, idx) => (
                      <li key={idx} className="leading-snug">{s}</li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-xs text-slate-500 italic">No strengths noted</p>
                )}
              </div>

              {/* Concerns */}
              <div className="p-4 rounded-lg bg-amber-950/20 border border-amber-900/40 space-y-2">
                <h4 className="text-xs font-bold text-amber-300 uppercase tracking-wider flex items-center">
                  <span className="mr-1.5">!</span> Areas for Review ({report.concerns?.length || 0})
                </h4>
                {report.concerns && report.concerns.length > 0 ? (
                  <ul className="space-y-1.5 text-xs text-slate-300 list-disc list-inside">
                    {report.concerns.map((c, idx) => (
                      <li key={idx} className="leading-snug">{c}</li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-xs text-slate-500 italic">No specific concerns identified</p>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Modal Footer */}
        <div className="pt-4 border-t border-slate-800 flex items-center justify-between">
          <div>
            {canReScreen && (
              <button
                type="button"
                disabled={reScreening || loading}
                onClick={handleReScreen}
                className="px-3.5 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium border border-slate-700 transition-colors disabled:opacity-50 flex items-center space-x-1.5"
              >
                {reScreening ? (
                  <>
                    <span className="inline-block w-3 h-3 border-2 border-slate-300 border-t-transparent rounded-full animate-spin" />
                    <span>Re-evaluating...</span>
                  </>
                ) : (
                  <>
                    <span>↻</span>
                    <span>Re-screen Candidate</span>
                  </>
                )}
              </button>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-sm font-medium transition-colors border border-slate-700"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
