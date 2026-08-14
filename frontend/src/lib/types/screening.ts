export type ScreeningStatus = "Pending" | "Completed" | "Failed";
export type ScreeningRecommendation = "Advance" | "Review";

export type ScreeningReportDto = {
  applicationId: string;
  score: number;
  recommendation: ScreeningRecommendation;
  summary: string;
  strengths: string[];
  concerns: string[];
  status: ScreeningStatus;
  failureReason: string | null;
  screenedAtUtc: string;
};
