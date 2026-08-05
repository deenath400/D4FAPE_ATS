import { NextRequest, NextResponse } from "next/server";
import { auth } from "@/lib/auth";
import { getBackendBaseUrl } from "@/lib/server/backend-invoke";

export async function GET(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const resolvedParams = await params;
  return proxyRequest(req, resolvedParams.path);
}

export async function POST(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const resolvedParams = await params;
  return proxyRequest(req, resolvedParams.path);
}

export async function PUT(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const resolvedParams = await params;
  return proxyRequest(req, resolvedParams.path);
}

export async function DELETE(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const resolvedParams = await params;
  return proxyRequest(req, resolvedParams.path);
}

async function proxyRequest(req: NextRequest, pathSegments: string[]) {
  const cleanBase = getBackendBaseUrl();
  const path = `/api/${pathSegments.join("/")}`;
  const url = `${cleanBase}${path}`;

  const session = await auth();
  const headers = new Headers();

  const contentType = req.headers.get("content-type");
  if (contentType) {
    headers.set("content-type", contentType);
  }

  if (session?.accessToken) {
    headers.set("authorization", `Bearer ${session.accessToken}`);
  }

  let body: string | undefined = undefined;
  if (req.method !== "GET" && req.method !== "HEAD") {
    body = await req.text();
  }

  try {
    const res = await fetch(url, {
      method: req.method,
      headers,
      body: body || undefined,
      cache: "no-store",
    });

    const resContentType = res.headers.get("content-type") || "application/json";
    const resText = await res.text();

    return new NextResponse(resText, {
      status: res.status,
      headers: {
        "content-type": resContentType,
      },
    });
  } catch {
    return NextResponse.json(
      {
        type: "https://d4fape.ats/errors/bff-proxy-failure",
        title: "Bad Gateway",
        status: 502,
        detail: "Failed to communicate with backend API service.",
        code: "bff.proxy.failure",
      },
      { status: 502, headers: { "content-type": "application/problem+json" } },
    );
  }
}
