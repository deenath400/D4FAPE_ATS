import { NextResponse } from "next/server";
import { auth } from "@/lib/auth";
import { isStaffRole } from "@/lib/auth-guards";

// Implements FR-9 / AC-14 / AC-15 — closes 0002's E-9. This is a UX convenience, not the
// security boundary: every `/api/requisitions*` call the staff pages make is still enforced
// server-side by the `RecruiterOnly`/`StaffOnly` policies (see `plan/hld.md` §8, R-2).
export default auth((req) => {
  const roles = req.auth?.user?.roles;

  if (isStaffRole(roles)) {
    return NextResponse.next();
  }

  const destination = req.auth?.user ? "/" : "/login";
  return NextResponse.redirect(new URL(destination, req.nextUrl));
});

export const config = {
  matcher: ["/staff/:path*"],
};
