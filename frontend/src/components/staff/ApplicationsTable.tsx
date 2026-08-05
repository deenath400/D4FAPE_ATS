import React from "react";
import type { StaffApplicationListItemDto } from "@/lib/types/application";

export type ApplicationsTableProps = {
  items: StaffApplicationListItemDto[];
};

// Presentational — staff per-Requisition Applications list (LLD §5.1, AC-16, AC-17, AC-18).
// No stage grouping or decisioning UI — out of scope for this spec (Clarification C-2).
export function ApplicationsTable({ items }: ApplicationsTableProps) {
  if (items.length === 0) {
    return (
      <div className="p-6 rounded-lg border border-slate-700 bg-slate-800/50 text-center text-slate-400">
        <p>No applications yet for this requisition.</p>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-lg border border-slate-700">
      <table className="w-full text-sm text-left">
        <thead className="bg-slate-800 text-slate-400 uppercase text-xs">
          <tr>
            <th className="px-4 py-3">Candidate</th>
            <th className="px-4 py-3">Email</th>
            <th className="px-4 py-3">Submitted</th>
            <th className="px-4 py-3">CV</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-800 bg-slate-900">
          {items.map((application) => (
            <tr key={application.id} className="hover:bg-slate-800/60">
              <td className="px-4 py-3 text-slate-100 font-medium">
                {application.candidate.firstName} {application.candidate.lastName}
              </td>
              <td className="px-4 py-3 text-slate-400">{application.candidate.email}</td>
              <td className="px-4 py-3 text-slate-400">
                {new Date(application.submittedAtUtc).toLocaleString()}
              </td>
              <td className="px-4 py-3">
                <a
                  href={`/api/bff/proxy${application.cvDownloadUrl}`}
                  className="text-emerald-400 hover:text-emerald-300 font-medium"
                >
                  Download
                </a>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
