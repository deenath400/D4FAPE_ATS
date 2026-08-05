import React from "react";
import { ServerStatusSection } from "@/components/ServerStatusSection";
import { ClientStatusPanel } from "@/components/ClientStatusPanel";
import { HeaderNav } from "@/components/HeaderNav";

export default function LandingPage() {
  return (
    <main className="min-h-screen bg-slate-900 text-slate-100 flex flex-col items-center justify-center p-6 md:p-12">
      <div className="max-w-4xl w-full space-y-8">
        <header className="flex flex-col md:flex-row items-center justify-between gap-4 pb-6 border-b border-slate-800">
          <div className="text-center md:text-left space-y-1">
            <h1 className="text-3xl md:text-4xl font-extrabold tracking-tight text-white">
              D4FAPE ATS
            </h1>
            <p className="text-sm text-slate-400">
              System Status &amp; Authentication Verification
            </p>
          </div>
          <HeaderNav />
        </header>

        <section className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <ServerStatusSection />
          <ClientStatusPanel />
        </section>

        <footer className="text-center text-xs text-slate-500 pt-8 border-t border-slate-800">
          Single-tenant Applicant Tracking System &bull; Project Scaffolding 0001
        </footer>
      </div>
    </main>
  );
}
