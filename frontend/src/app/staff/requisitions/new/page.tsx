import React from "react";
import { RequisitionForm } from "@/components/staff/RequisitionForm";

export const metadata = {
  title: "New Requisition | D4FAPE ATS",
};

// AC-1 — Recruiter creates a Requisition, which is created in `draft` status.
export default function NewRequisitionPage() {
  return (
    <div className="space-y-6 max-w-2xl">
      <h2 className="text-2xl font-bold text-white">New Requisition</h2>
      <RequisitionForm mode="create" />
    </div>
  );
}
