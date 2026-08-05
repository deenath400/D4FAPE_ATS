import React from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { HeaderNav } from "@/components/HeaderNav";
import { auth } from "@/lib/auth";
import { invokeBackend, BackendInvokeError } from "@/lib/server/backend-invoke";
import { isCandidateRole } from "@/lib/auth-guards";
import type { PublicRequisitionDto } from "@/lib/types/requisition";

// Public job detail (AC-21, AC-22). A draft/closed/missing id 404s identically (E-10) —
// `notFound()` renders Next.js's default not-found page, no bespoke copy required (LLD §5.3).
export default async function JobDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  let job: PublicRequisitionDto;
  try {
    job = await invokeBackend<PublicRequisitionDto>({ path: `/api/public/requisitions/${id}` });
  } catch (err) {
    if (err instanceof BackendInvokeError && err.status === 404) {
      notFound();
    }
    throw err;
  }

  // Session-aware "Apply" call to action (T-31): unauthenticated visitors are sent to sign in
  // and back; authenticated non-Candidates (staff) see no apply affordance at all.
  const session = await auth();
  const isCandidate = isCandidateRole(session?.user?.roles);

  return (
    <main className="min-h-screen bg-slate-900 text-slate-100 p-6 md:p-12">
      <div className="max-w-3xl mx-auto space-y-8">
        <header className="flex flex-col md:flex-row items-center justify-between gap-4 pb-6 border-b border-slate-800">
          <Link href="/jobs" className="text-sm text-emerald-400 hover:text-emerald-300">
            &larr; Back to all roles
          </Link>
          <HeaderNav />
        </header>

        <article className="space-y-4">
          <h1 className="text-3xl font-bold text-white">{job.title}</h1>
          <p className="text-xs uppercase tracking-wider text-slate-500">
            Updated {new Date(job.updatedAtUtc).toLocaleDateString()}
          </p>
          <p className="whitespace-pre-wrap text-slate-300 leading-relaxed">{job.description}</p>

          {!session?.user && (
            <Link
              href={`/login?callbackUrl=${encodeURIComponent(`/jobs/${id}/apply`)}`}
              className="inline-block px-4 py-2 font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg shadow-md transition-all text-sm"
            >
              Sign In to Apply
            </Link>
          )}

          {session?.user && isCandidate && (
            <Link
              href={`/jobs/${id}/apply`}
              className="inline-block px-4 py-2 font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg shadow-md transition-all text-sm"
            >
              Apply Now
            </Link>
          )}
        </article>
      </div>
    </main>
  );
}
