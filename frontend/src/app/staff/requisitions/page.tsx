import React from "react";
import Link from "next/link";
import { invokeBackend } from "@/lib/server/backend-invoke";
import type { RequisitionDto } from "@/lib/types/requisition";

export const metadata = {
  title: "Requisitions | D4FAPE ATS",
};

// Staff data is per-session and always live — never statically prerendered at build time.
export const dynamic = "force-dynamic";

const STATUS_STYLES: Record<RequisitionDto["status"], string> = {
  draft: "bg-slate-700/80 text-slate-300 border-slate-600",
  published: "bg-emerald-950/80 border-emerald-700/80 text-emerald-300",
  closed: "bg-red-950/60 border-red-800/80 text-red-300",
};

function StatusBadge({ status }: { status: RequisitionDto["status"] }) {
  return (
    <span
      className={`px-2 py-0.5 text-xs font-semibold rounded-full border capitalize ${STATUS_STYLES[status]}`}
    >
      {status}
    </span>
  );
}

// Staff requisition list (AC-12) — visible to Recruiter and HiringManager alike (`StaffOnly`).
export default async function StaffRequisitionsPage() {
  const requisitions = await invokeBackend<RequisitionDto[]>({ path: "/api/requisitions" });

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-2xl font-bold text-white">Requisitions</h2>
        <Link
          href="/staff/requisitions/new"
          className="px-3.5 py-1.5 font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg shadow-md transition-all text-sm"
        >
          New Requisition
        </Link>
      </div>

      {requisitions.length === 0 ? (
        <div className="p-6 rounded-lg border border-slate-700 bg-slate-800/50 text-center text-slate-400">
          <p>No requisitions yet — create one to get started.</p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-lg border border-slate-700">
          <table className="w-full text-sm text-left">
            <thead className="bg-slate-800 text-slate-400 uppercase text-xs">
              <tr>
                <th className="px-4 py-3">Title</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Updated</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800 bg-slate-900">
              {requisitions.map((requisition) => (
                <tr key={requisition.id} className="hover:bg-slate-800/60">
                  <td className="px-4 py-3">
                    <Link
                      href={`/staff/requisitions/${requisition.id}`}
                      className="text-slate-100 hover:text-emerald-400 font-medium"
                    >
                      {requisition.title}
                    </Link>
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={requisition.status} />
                  </td>
                  <td className="px-4 py-3 text-slate-400">
                    {new Date(requisition.updatedAtUtc).toLocaleString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
