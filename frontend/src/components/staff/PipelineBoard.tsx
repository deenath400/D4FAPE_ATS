"use client";

import React, { useState } from "react";
import Link from "next/link";
import type { PipelineBoardDto, StageDto } from "@/lib/types/pipeline";
import { MoveApplicationControl } from "@/components/staff/MoveApplicationControl";
import { RejectApplicationControl } from "@/components/staff/RejectApplicationControl";
import { ScreeningBadge } from "@/components/staff/ScreeningBadge";
import { ScreeningReportModal } from "@/components/staff/ScreeningReportModal";

export type PipelineBoardProps = {
  requisitionId: string;
  board: PipelineBoardDto;
  canWrite: boolean;
};

// Grouped-by-Stage board + a separate Rejected column (LLD §5.1, AC-11, AC-18, AC-19). Every
// configured Stage renders even at zero count (AC-19) since it iterates `board.stages`, not the
// (possibly empty) Application list. `canWrite=false` (HiringManager, FR-20) hides the
// move/reject controls; presentational — no fetch of its own, server-rendered by the pipeline
// page and refreshed by each control's own `router.refresh()`.
export function PipelineBoard({ requisitionId, board, canWrite }: PipelineBoardProps) {
  const [selectedApp, setSelectedApp] = useState<{ id: string; name: string } | null>(null);

  const stages = [...board.stages].sort((a, b) => a.sortOrder - b.sortOrder);
  const targetStages: StageDto[] = stages.map((s) => ({
    id: s.stageId,
    requisitionId,
    name: s.stageName,
    sortOrder: s.sortOrder,
  }));

  return (
    <>
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        {stages.map((group) => (
          <div
            key={group.stageId}
            className="rounded-lg border border-slate-700 bg-slate-900 flex flex-col"
          >
            <div className="px-4 py-3 border-b border-slate-800 flex items-center justify-between">
              <h3 className="font-semibold text-slate-100">{group.stageName}</h3>
              <span className="text-xs font-medium text-slate-400">{group.count}</span>
            </div>
            <div className="p-3 space-y-3 flex-1">
              {group.applications.length === 0 ? (
                <p className="text-xs text-slate-500 text-center py-4">No applicants</p>
              ) : (
                group.applications.map((application) => (
                  <div
                    key={application.applicationId}
                    className="p-3 rounded-lg bg-slate-800/70 border border-slate-700 space-y-2"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <Link
                        href={`/staff/applications/${application.applicationId}`}
                        className="block text-sm font-medium text-slate-100 hover:text-emerald-400"
                      >
                        {application.candidateFirstName} {application.candidateLastName}
                      </Link>
                      <ScreeningBadge
                        score={application.screeningScore}
                        recommendation={application.screeningRecommendation}
                        status={application.screeningStatus}
                        onClick={() =>
                          setSelectedApp({
                            id: application.applicationId,
                            name: `${application.candidateFirstName} ${application.candidateLastName}`,
                          })
                        }
                      />
                    </div>
                    <p className="text-xs text-slate-500">{application.candidateEmail}</p>
                    {canWrite && (
                      <div className="space-y-2 pt-1">
                        <MoveApplicationControl
                          applicationId={application.applicationId}
                          currentStageId={group.stageId}
                          stages={targetStages}
                        />
                        <RejectApplicationControl applicationId={application.applicationId} />
                      </div>
                    )}
                  </div>
                ))
              )}
            </div>
          </div>
        ))}

        <div className="rounded-lg border border-red-900/60 bg-red-950/20 flex flex-col">
          <div className="px-4 py-3 border-b border-red-900/60 flex items-center justify-between">
            <h3 className="font-semibold text-red-200">Rejected</h3>
            <span className="text-xs font-medium text-red-300">{board.rejected.count}</span>
          </div>
          <div className="p-3 space-y-3 flex-1">
            {board.rejected.applications.length === 0 ? (
              <p className="text-xs text-red-400/70 text-center py-4">No rejected applicants</p>
            ) : (
              board.rejected.applications.map((application) => (
                <div
                  key={application.applicationId}
                  className="p-3 rounded-lg bg-slate-800/70 border border-red-900/40 space-y-1"
                >
                  <div className="flex items-start justify-between gap-2">
                    <Link
                      href={`/staff/applications/${application.applicationId}`}
                      className="block text-sm font-medium text-slate-100 hover:text-emerald-400"
                    >
                      {application.candidateFirstName} {application.candidateLastName}
                    </Link>
                    <ScreeningBadge
                      score={application.screeningScore}
                      recommendation={application.screeningRecommendation}
                      status={application.screeningStatus}
                      onClick={() =>
                        setSelectedApp({
                          id: application.applicationId,
                          name: `${application.candidateFirstName} ${application.candidateLastName}`,
                        })
                      }
                    />
                  </div>
                  <p className="text-xs text-slate-500">{application.candidateEmail}</p>
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      {selectedApp && (
        <ScreeningReportModal
          applicationId={selectedApp.id}
          candidateName={selectedApp.name}
          isOpen={true}
          onClose={() => setSelectedApp(null)}
          canReScreen={canWrite}
        />
      )}
    </>
  );
}
