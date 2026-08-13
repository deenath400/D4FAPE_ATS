import React from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { auth } from "@/lib/auth";
import { invokeBackend, BackendInvokeError } from "@/lib/server/backend-invoke";
import { isRecruiter } from "@/lib/auth-guards";
import type { StageDto } from "@/lib/types/pipeline";
import { StageConfigPanel } from "@/components/staff/StageConfigPanel";

export const metadata = {
  title: "Stage Configuration | D4FAPE ATS",
};

// Staff data is per-session and always live — never statically prerendered at build time.
export const dynamic = "force-dynamic";

// Stage-configuration screen (LLD §5.1, AC-9). A HiringManager session reaches the same page
// (StaffOnly read, FR-6, FR-20) but `canWrite=false` hides every write affordance, mirroring
// the requisition detail page's `RequisitionLifecycleActions` gating.
export default async function StageConfigPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  let stages: StageDto[];
  try {
    stages = await invokeBackend<StageDto[]>({ path: `/api/requisitions/${id}/stages` });
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
        <h2 className="text-2xl font-bold text-white">Pipeline Stages</h2>
        <Link
          href={`/staff/requisitions/${id}`}
          className="text-sm text-emerald-400 hover:text-emerald-300"
        >
          &larr; Back to requisition
        </Link>
      </div>

      <StageConfigPanel requisitionId={id} stages={stages} canWrite={canWrite} />
    </div>
  );
}
