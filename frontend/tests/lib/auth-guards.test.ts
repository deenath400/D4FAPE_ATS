import { describe, it, expect } from "vitest";
import { isStaffRole, isRecruiter } from "../../src/lib/auth-guards";

describe("isStaffRole", () => {
  it("returns true for a Recruiter role", () => {
    expect(isStaffRole(["Recruiter"])).toBe(true);
  });

  it("returns true for a HiringManager role", () => {
    expect(isStaffRole(["HiringManager"])).toBe(true);
  });

  it("returns false for a Candidate role", () => {
    expect(isStaffRole(["Candidate"])).toBe(false);
  });

  it("returns false when no roles are present", () => {
    expect(isStaffRole([])).toBe(false);
    expect(isStaffRole(undefined)).toBe(false);
    expect(isStaffRole(null)).toBe(false);
  });
});

describe("isRecruiter", () => {
  it("returns true only for a Recruiter role", () => {
    expect(isRecruiter(["Recruiter"])).toBe(true);
  });

  it("returns false for a HiringManager role", () => {
    expect(isRecruiter(["HiringManager"])).toBe(false);
  });

  it("returns false for a Candidate role or no roles", () => {
    expect(isRecruiter(["Candidate"])).toBe(false);
    expect(isRecruiter(undefined)).toBe(false);
  });
});
