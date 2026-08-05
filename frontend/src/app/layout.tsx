import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "D4FAPE ATS",
  description: "Applicant Tracking System",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="bg-slate-900 text-slate-100 min-h-screen antialiased">{children}</body>
    </html>
  );
}
