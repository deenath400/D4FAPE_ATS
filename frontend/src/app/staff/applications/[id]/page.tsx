import React from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { invokeBackend, BackendInvokeError } from "@/lib/server/backend-invoke";
import type { StageTransitionDto } from "@/lib/types/pipeline";
import { TransitionHistoryList } from "@/components/staff/TransitionHistoryList";

export const metadata = {
  title: "Application Transition History | D4FAPE ATS",
};

// Staff data is per-session and always live — never statically prerendered at build time.
export const dynamic = "force-dynamic";

// Application detail: full transition history (LLD §5.1, AC-20, AC-21, FR-16). A HiringManager
// session reaches the same page (StaffOnly, FR-20). A missing Application id 404s, the same
// `notFound()` pattern used across `ui/staff`.
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

  return (
    <div className="space-y-6 max-w-2xl">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-white">Transition History</h2>
        <Link
          href="/staff/requisitions"
          className="text-sm text-emerald-400 hover:text-emerald-300"
        >
          &larr; Back to requisitions
        </Link>
      </div>

      <TransitionHistoryList items={transitions} />
    </div>
  );
}
