import React from "react";
import Link from "next/link";
import { HeaderNav } from "@/components/HeaderNav";

// Replaces `0001`'s non-routing `(staff)` placeholder with a real `/staff` URL segment
// (`plan/hld.md` D-4) — `middleware.ts` gates every route under this layout (FR-9).
export default function StaffLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-slate-900 text-slate-100">
      <header className="flex flex-col md:flex-row items-center justify-between gap-4 px-6 py-4 border-b border-slate-800">
        <div>
          <p className="text-xs uppercase tracking-wider text-slate-500">D4FAPE ATS</p>
          <Link href="/staff/requisitions" className="text-lg font-semibold text-white hover:text-emerald-400">
            Staff Workspace
          </Link>
        </div>
        <HeaderNav />
      </header>
      <main className="p-6 md:p-10 max-w-5xl mx-auto">{children}</main>
    </div>
  );
}
