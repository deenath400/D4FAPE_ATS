import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { RegisterForm } from "../../src/components/auth/RegisterForm";
import * as nextAuth from "next-auth/react";

const mockPush = vi.fn();
const mockRefresh = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: mockPush,
    refresh: mockRefresh,
  }),
}));

vi.mock("next-auth/react", () => ({
  signIn: vi.fn(),
}));

describe("RegisterForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders all form inputs and submit button", () => {
    render(<RegisterForm />);

    expect(screen.getByLabelText(/first name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/last name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/email address/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /create account/i })).toBeInTheDocument();
  });

  it("submits registration and automatically signs in on success", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 201,
      json: async () => ({ id: "123", email: "jane@example.com" }),
    } as Response);

    vi.mocked(nextAuth.signIn).mockResolvedValue({
      error: null,
      status: 200,
      ok: true,
      url: "/",
    });

    render(<RegisterForm />);

    fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: "Jane" } });
    fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: "Doe" } });
    fireEvent.change(screen.getByLabelText(/email address/i), {
      target: { value: "jane@example.com" },
    });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: "Password123!" },
    });

    fireEvent.click(screen.getByRole("button", { name: /create account/i }));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/bff/proxy/auth/register",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({
            firstName: "Jane",
            lastName: "Doe",
            email: "jane@example.com",
            password: "Password123!",
          }),
        }),
      );
    });

    await waitFor(() => {
      expect(nextAuth.signIn).toHaveBeenCalledWith("credentials", {
        email: "jane@example.com",
        password: "Password123!",
        redirect: false,
      });
      expect(mockPush).toHaveBeenCalledWith("/");
    });
  });

  it("displays error banner when registration endpoint returns duplicate email error", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({
        detail: "An account with this email address already exists.",
        code: "auth.register.duplicate-email",
      }),
    } as Response);

    render(<RegisterForm />);

    fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: "Jane" } });
    fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: "Doe" } });
    fireEvent.change(screen.getByLabelText(/email address/i), {
      target: { value: "dup@example.com" },
    });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: "Password123!" },
    });

    fireEvent.click(screen.getByRole("button", { name: /create account/i }));

    await waitFor(() => {
      expect(
        screen.getByText("An account with this email address already exists."),
      ).toBeInTheDocument();
    });
  });

  it("lists the specific validation reasons when password fails Identity rules", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({
        title: "Validation",
        detail: "User registration failed validation.",
        code: "auth.error",
        errors: {
          PasswordTooShort: ["Passwords must be at least 8 characters."],
          PasswordRequiresUpper: ["Passwords must have at least one uppercase ('A'-'Z')."],
        },
      }),
    } as Response);

    render(<RegisterForm />);

    fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: "Jane" } });
    fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: "Doe" } });
    fireEvent.change(screen.getByLabelText(/email address/i), {
      target: { value: "jane@example.com" },
    });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: "abc123" } });

    fireEvent.click(screen.getByRole("button", { name: /create account/i }));

    await waitFor(() => {
      expect(screen.getByText("User registration failed validation.")).toBeInTheDocument();
      expect(
        screen.getByText("Passwords must be at least 8 characters."),
      ).toBeInTheDocument();
      expect(
        screen.getByText("Passwords must have at least one uppercase ('A'-'Z')."),
      ).toBeInTheDocument();
    });
  });
});
