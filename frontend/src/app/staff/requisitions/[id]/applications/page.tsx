import React from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { auth } from "@/lib/auth";
import { isRecruiter } from "@/lib/auth-guards";
import { invokeBackend, BackendInvokeError } from "@/lib/server/backend-invoke";
import type { StaffApplicationListItemDto } from "@/lib/types/application";
import { ApplicationsTable } from "@/components/staff/ApplicationsTable";

export const metadata = {
  title: "Applications | D4FAPE ATS",
};

// Staff data is per-session and always live — never statically prerendered at build time.
export const dynamic = "force-dynamic";

// Staff per-Requisition Applications list (AC-16, AC-17, AC-18, AC-19). A missing Requisition
// id 404s (E-5) — the same `notFound()` pattern as the staff requisition detail page.
export default async function StaffApplicationsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  let applications: StaffApplicationListItemDto[];
  try {
    applications = await invokeBackend<StaffApplicationListItemDto[]>({
      path: `/api/requisitions/${id}/applications`,
    });
  } catch (err) {
    if (err instanceof BackendInvokeError && err.status === 404) {
      notFound();
    }
    throw err;
  }

  const session = await auth();
  const canReScreen = isRecruiter(session?.user?.roles);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-white">Applications</h2>
        <Link
          href={`/staff/requisitions/${id}`}
          className="text-sm text-emerald-400 hover:text-emerald-300"
        >
          &larr; Back to requisition
        </Link>
      </div>

      <ApplicationsTable items={applications} canReScreen={canReScreen} />
    </div>
  );
}
