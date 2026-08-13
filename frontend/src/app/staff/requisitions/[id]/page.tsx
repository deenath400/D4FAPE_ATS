import React from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { auth } from "@/lib/auth";
import { invokeBackend, BackendInvokeError } from "@/lib/server/backend-invoke";
import { isRecruiter } from "@/lib/auth-guards";
import type { RequisitionDto } from "@/lib/types/requisition";
import { RequisitionForm } from "@/components/staff/RequisitionForm";
import { RequisitionLifecycleActions } from "@/components/staff/RequisitionLifecycleActions";

export const metadata = {
  title: "Requisition Detail | D4FAPE ATS",
};

// Staff detail/edit/lifecycle page (AC-3–AC-11). A HiringManager session reaches the same
// page (read access, clarification C-2) but `canWrite=false` hides the lifecycle buttons.
export default async function StaffRequisitionDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  let requisition: RequisitionDto;
  try {
    requisition = await invokeBackend<RequisitionDto>({ path: `/api/requisitions/${id}` });
  } catch (err) {
    if (err instanceof BackendInvokeError && err.status === 404) {
      notFound();
    }
    throw err;
  }

  const session = await auth();
  const canWrite = isRecruiter(session?.user?.roles);

  return (
    <div className="space-y-8 max-w-2xl">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-white">{requisition.title}</h2>
        <div className="flex items-center gap-4">
          <Link
            href={`/staff/requisitions/${requisition.id}/applications`}
            className="text-sm font-medium text-emerald-400 hover:text-emerald-300"
          >
            View Applications
          </Link>
          <Link
            href={`/staff/requisitions/${requisition.id}/stages`}
            className="text-sm font-medium text-emerald-400 hover:text-emerald-300"
          >
            Configure Stages
          </Link>
          <Link
            href={`/staff/requisitions/${requisition.id}/pipeline`}
            className="text-sm font-medium text-emerald-400 hover:text-emerald-300"
          >
            View Pipeline
          </Link>
          <span className="text-xs uppercase tracking-wider text-slate-400">
            {requisition.status}
          </span>
        </div>
      </div>

      <RequisitionLifecycleActions
        requisitionId={requisition.id}
        status={requisition.status}
        canWrite={canWrite}
      />

      <RequisitionForm
        mode="edit"
        requisitionId={requisition.id}
        initialTitle={requisition.title}
        initialDescription={requisition.description}
      />
    </div>
  );
}
