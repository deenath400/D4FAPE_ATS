import React from "react";
import Link from "next/link";
import { LoginForm } from "@/components/auth/LoginForm";

export const metadata = {
  title: "Sign In | D4FAPE ATS",
  description: "Sign in to your D4FAPE ATS account",
};

export default function LoginPage() {
  return (
    <main className="min-h-screen flex flex-col items-center justify-center p-4 bg-slate-900 text-slate-100">
      <div className="mb-8 text-center">
        <h1 className="text-3xl font-bold tracking-tight text-white mb-2">Welcome Back</h1>
        <p className="text-sm text-slate-400">
          Don&apos;t have an account yet?{" "}
          <Link href="/register" className="text-emerald-400 hover:text-emerald-300 underline">
            Register here
          </Link>
        </p>
      </div>

      <LoginForm />
    </main>
  );
}
