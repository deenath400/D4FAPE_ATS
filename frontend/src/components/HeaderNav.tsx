"use client";

import React from "react";
import Link from "next/link";
import { useSession, signOut } from "next-auth/react";
import { isStaffRole } from "@/lib/auth-guards";

export function HeaderNav() {
  const { data: session, status } = useSession();

  if (status === "loading") {
    return (
      <div className="flex items-center gap-4 animate-pulse">
        <div className="h-8 w-20 bg-slate-800 rounded-lg"></div>
        <div className="h-8 w-24 bg-slate-800 rounded-lg"></div>
      </div>
    );
  }

  if (session?.user) {
    const roles = session.user.roles || [];
    const roleBadge = roles[0] || "User";
    const displayName =
      session.user.firstName && session.user.lastName
        ? `${session.user.firstName} ${session.user.lastName}`
        : session.user.email;

    return (
      <div className="flex items-center gap-4">
        <nav className="flex items-center gap-3 text-sm">
          <Link href="/jobs" className="text-slate-300 hover:text-white transition-all">
            Browse Jobs
          </Link>
          {isStaffRole(roles) && (
            <Link
              href="/staff/requisitions"
              className="text-slate-300 hover:text-white transition-all"
            >
              Staff Workspace
            </Link>
          )}
        </nav>
        <div className="flex items-center gap-2 text-sm">
          <span className="text-slate-200 font-medium">{displayName}</span>
          <span className="px-2 py-0.5 text-xs font-semibold bg-emerald-950/80 border border-emerald-700/80 text-emerald-300 rounded-full">
            {roleBadge}
          </span>
        </div>
        <button
          onClick={() => signOut({ callbackUrl: "/" })}
          className="px-3 py-1.5 text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white border border-slate-700 rounded-lg transition-all"
        >
          Sign Out
        </button>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-3 text-sm">
      <Link
        href="/jobs"
        className="px-3 py-1.5 font-medium text-slate-300 hover:text-white hover:bg-slate-800 rounded-lg transition-all"
      >
        Browse Jobs
      </Link>
      <Link
        href="/login"
        className="px-3 py-1.5 font-medium text-slate-300 hover:text-white hover:bg-slate-800 rounded-lg transition-all"
      >
        Sign In
      </Link>
      <Link
        href="/register"
        className="px-3.5 py-1.5 font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg shadow-md transition-all"
      >
        Register
      </Link>
    </div>
  );
}
