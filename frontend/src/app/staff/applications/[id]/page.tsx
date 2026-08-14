import React from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { auth } from "@/lib/auth";
import { isRecruiter } from "@/lib/auth-guards";
import { invokeBackend, BackendInvokeError } from "@/lib/server/backend-invoke";
import type { StageTransitionDto } from "@/lib/types/pipeline";
import type { ScreeningReportDto } from "@/lib/types/screening";
import { TransitionHistoryList } from "@/components/staff/TransitionHistoryList";
import { ScreeningReportCard } from "@/components/staff/ScreeningReportCard";

export const metadata = {
  title: "Application Detail | D4FAPE ATS",
};

// Staff data is per-session and always live — never statically prerendered at build time.
export const dynamic = "force-dynamic";

export default async function StaffApplicationDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  let transitions: StageTransitionDto[];
  try {
    transitions = await invokeBackend<StageTransitionDto[]>({
      path: `/api/applications/${id}/transitions`,
    });
  } catch (err) {
    if (err instanceof BackendInvokeError && err.status === 404) {
      notFound();
    }
    throw err;
  }

  let screeningReport: ScreeningReportDto | null = null;
  try {
    screeningReport = await invokeBackend<ScreeningReportDto>({
      path: `/api/staff/applications/${id}/screening-report`,
    });
  } catch {
    // 404 if not screened yet
  }

  const session = await auth();
  const canReScreen = isRecruiter(session?.user?.roles);

  return (
    <div className="space-y-8 max-w-3xl">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-white">Application Details</h2>
        <Link
          href="/staff/requisitions"
          className="text-sm text-emerald-400 hover:text-emerald-300"
        >
          &larr; Back to requisitions
        </Link>
      </div>

      <div className="space-y-4">
        <h3 className="text-lg font-semibold text-slate-100">Candidate Screening</h3>
        <ScreeningReportCard
          applicationId={id}
          report={screeningReport}
          canReScreen={canReScreen}
        />
      </div>

      <div className="space-y-4">
        <h3 className="text-lg font-semibold text-slate-100">Transition History</h3>
        <TransitionHistoryList items={transitions} />
      </div>
    </div>
  );
}
