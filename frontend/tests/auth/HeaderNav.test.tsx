import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { HeaderNav } from "../../src/components/HeaderNav";
import * as nextAuth from "next-auth/react";

vi.mock("next-auth/react", () => ({
  useSession: vi.fn(),
  signOut: vi.fn(),
}));

describe("HeaderNav", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders Sign In and Register links when unauthenticated", () => {
    vi.mocked(nextAuth.useSession).mockReturnValue({
      data: null,
      status: "unauthenticated",
      update: vi.fn(),
    });

    render(<HeaderNav />);

    expect(screen.getByRole("link", { name: /sign in/i })).toHaveAttribute("href", "/login");
    expect(screen.getByRole("link", { name: /register/i })).toHaveAttribute("href", "/register");
  });

  it("renders user full name, role badge, and Sign Out button when authenticated", () => {
    vi.mocked(nextAuth.useSession).mockReturnValue({
      data: {
        user: {
          id: "123",
          email: "jane@example.com",
          firstName: "Jane",
          lastName: "Doe",
          roles: ["Candidate"],
        },
        expires: "2099-01-01T00:00:00.000Z",
      },
      status: "authenticated",
      update: vi.fn(),
    });

    render(<HeaderNav />);

    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText("Candidate")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /sign out/i })).toBeInTheDocument();
  });
});
