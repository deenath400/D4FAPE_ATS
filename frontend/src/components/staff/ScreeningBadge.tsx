import React from "react";
import type { ScreeningRecommendation, ScreeningStatus } from "@/lib/types/screening";

export type ScreeningBadgeProps = {
  score?: number | null;
  recommendation?: ScreeningRecommendation | string | null;
  status?: ScreeningStatus | string | null;
  onClick?: () => void;
};

export function ScreeningBadge({ score, recommendation, status, onClick }: ScreeningBadgeProps) {
  if (!status && !recommendation && score == null) {
    return null;
  }

  let badgeStyle = "bg-slate-800 text-slate-400 border-slate-700";
  let label = "Pending";

  if (status === "Pending") {
    badgeStyle = "bg-sky-950/60 text-sky-300 border-sky-800/60 animate-pulse";
    label = "Screening...";
  } else if (status === "Failed") {
    badgeStyle = "bg-rose-950/60 text-rose-300 border-rose-800/60";
    label = "Screening Failed";
  } else if (recommendation === "Advance") {
    badgeStyle = "bg-emerald-950/70 text-emerald-300 border-emerald-800/60";
    label = score != null ? `${score} · Advance` : "Advance";
  } else if (recommendation === "Review") {
    badgeStyle = "bg-amber-950/70 text-amber-300 border-amber-800/60";
    label = score != null ? `${score} · Review` : "Review";
  } else if (score != null) {
    label = `${score}/100`;
  }

  const Tag = onClick ? "button" : "span";

  return (
    <Tag
      type={onClick ? "button" : undefined}
      onClick={onClick}
      className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border transition-colors ${badgeStyle} ${
        onClick ? "hover:brightness-125 cursor-pointer" : ""
      }`}
      title={onClick ? "Click to view AI screening report" : undefined}
    >
      <span className="mr-1 inline-block w-1.5 h-1.5 rounded-full bg-current opacity-75" />
      {label}
    </Tag>
  );
}
