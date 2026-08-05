import React from "react";
import Link from "next/link";
import { RegisterForm } from "@/components/auth/RegisterForm";

export const metadata = {
  title: "Register | D4FAPE ATS",
  description: "Create a candidate account on D4FAPE ATS",
};

export default function RegisterPage() {
  return (
    <main className="min-h-screen flex flex-col items-center justify-center p-4 bg-slate-900 text-slate-100">
      <div className="mb-8 text-center">
        <h1 className="text-3xl font-bold tracking-tight text-white mb-2">Join D4FAPE ATS</h1>
        <p className="text-sm text-slate-400">
          Already have an account?{" "}
          <Link href="/login" className="text-emerald-400 hover:text-emerald-300 underline">
            Sign in here
          </Link>
        </p>
      </div>

      <RegisterForm />
    </main>
  );
}
