import React from "react";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { HeaderNav } from "@/components/HeaderNav";
import { auth } from "@/lib/auth";
import { invokeBackend, BackendInvokeError } from "@/lib/server/backend-invoke";
import { isCandidateRole } from "@/lib/auth-guards";
import type { PublicRequisitionDto } from "@/lib/types/requisition";
import { ApplicationForm } from "@/components/portal/ApplicationForm";

export const metadata = {
  title: "Apply | D4FAPE ATS",
};

// Apply page (AC-1, AC-5, AC-6, AC-7). A draft/closed/missing Requisition 404s identically to
// the public detail page (E-2, LLD §5.3) — no stale-form success against a no-longer-public
// Requisition. Unauthenticated candidates are redirected to sign in and back (LLD §5.3);
// authenticated non-Candidates see an inline message instead of the form.
export default async function ApplyPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  const session = await auth();
  if (!session?.user) {
    redirect(`/login?callbackUrl=${encodeURIComponent(`/jobs/${id}/apply`)}`);
  }

  let job: PublicRequisitionDto;
  try {
    job = await invokeBackend<PublicRequisitionDto>({ path: `/api/public/requisitions/${id}` });
  } catch (err) {
    if (err instanceof BackendInvokeError && err.status === 404) {
      notFound();
    }
    throw err;
  }

  const isCandidate = isCandidateRole(session.user.roles);

  return (
    <main className="min-h-screen bg-slate-900 text-slate-100 p-6 md:p-12">
      <div className="max-w-2xl mx-auto space-y-8">
        <header className="flex flex-col md:flex-row items-center justify-between gap-4 pb-6 border-b border-slate-800">
          <Link href={`/jobs/${id}`} className="text-sm text-emerald-400 hover:text-emerald-300">
            &larr; Back to role details
          </Link>
          <HeaderNav />
        </header>

        <div className="space-y-2">
          <h1 className="text-2xl font-bold text-white">Apply &mdash; {job.title}</h1>
          <p className="text-sm text-slate-400">
            Attach your CV as a PDF (max 5 MB) to apply for this role.
          </p>
        </div>

        {isCandidate ? (
          <ApplicationForm requisitionId={id} requisitionTitle={job.title} />
        ) : (
          <p
            role="alert"
            className="p-4 bg-red-950/70 border border-red-800/80 rounded-xl text-red-200 text-sm"
          >
            Only candidates can apply.
          </p>
        )}
      </div>
    </main>
  );
}
