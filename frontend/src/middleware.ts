import { NextResponse } from "next/server";
import { auth } from "@/lib/auth";
import { isStaffRole, isCandidateRole } from "@/lib/auth-guards";

// Implements FR-9 / AC-14 / AC-15 (0003, closes 0002's E-9) for `/staff/*`, and FR-6 (0004)
// for `/applications/*`. Both are a UX convenience, not the security boundary: every backend
// call each surface makes is still enforced server-side by its own authorization policy (see
// `plan/hld.md` §8, R-2).
export default auth((req) => {
  const roles = req.auth?.user?.roles;
  const pathname = req.nextUrl.pathname;
  const destination = req.auth?.user ? "/" : "/login";

  if (pathname.startsWith("/staff")) {
    return isStaffRole(roles)
      ? NextResponse.next()
      : NextResponse.redirect(new URL(destination, req.nextUrl));
  }

  if (pathname.startsWith("/applications")) {
    return isCandidateRole(roles)
      ? NextResponse.next()
      : NextResponse.redirect(new URL(destination, req.nextUrl));
  }

  return NextResponse.next();
});

export const config = {
  matcher: ["/staff/:path*", "/applications/:path*"],
};
