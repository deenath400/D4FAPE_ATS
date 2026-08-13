import React from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { auth } from "@/lib/auth";
import { invokeBackend, BackendInvokeError } from "@/lib/server/backend-invoke";
import { isRecruiter } from "@/lib/auth-guards";
import type { PipelineBoardDto } from "@/lib/types/pipeline";
import { PipelineBoard } from "@/components/staff/PipelineBoard";

export const metadata = {
  title: "Pipeline Board | D4FAPE ATS",
};

// Staff data is per-session and always live — never statically prerendered at build time.
export const dynamic = "force-dynamic";

// Staff pipeline board grouped by Stage (LLD §5.1, AC-18, AC-19, S-4). A HiringManager session
// reaches the same page (StaffOnly read, FR-20) but `canWrite=false` hides the move/reject
// controls. A missing Requisition id 404s, the same `notFound()` pattern used across `ui/staff`.
export default async function PipelineBoardPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  let board: PipelineBoardDto;
  try {
    board = await invokeBackend<PipelineBoardDto>({ path: `/api/requisitions/${id}/pipeline` });
  } catch (err) {
    if (err instanceof BackendInvokeError && err.status === 404) {
      notFound();
    }
    throw err;
  }

  const session = await auth();
  const canWrite = isRecruiter(session?.user?.roles);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-white">Pipeline Board</h2>
        <Link
          href={`/staff/requisitions/${id}`}
          className="text-sm text-emerald-400 hover:text-emerald-300"
        >
          &larr; Back to requisition
        </Link>
      </div>

      <PipelineBoard requisitionId={id} board={board} canWrite={canWrite} />
    </div>
  );
}
