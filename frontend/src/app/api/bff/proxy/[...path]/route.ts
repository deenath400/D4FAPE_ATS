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

  // Binary-safe passthrough (T-25, `hld.md` D-4). `text()` UTF-8-decodes the body, which
  // corrupts a binary CV (multipart upload request, PDF download response) in either
  // direction. `ArrayBuffer` is a strict generalisation of the previous text() behaviour —
  // every existing JSON caller round-trips through it unchanged.
  let body: ArrayBuffer | undefined = undefined;
  if (req.method !== "GET" && req.method !== "HEAD") {
    body = await req.arrayBuffer();
  }

  try {
    const res = await fetch(url, {
      method: req.method,
      headers,
      body,
      cache: "no-store",
    });

    const resContentType = res.headers.get("content-type") || "application/json";
    const resBody = await res.arrayBuffer();

    const responseHeaders: Record<string, string> = {
      "content-type": resContentType,
    };

    // Forwarded so a CV download's filename survives the proxy (AC-14, AC-20) — the backend
    // sets this via `Results.File(...)`, never hand-constructed here.
    const contentDisposition = res.headers.get("content-disposition");
    if (contentDisposition) {
      responseHeaders["content-disposition"] = contentDisposition;
    }

    return new NextResponse(resBody, {
      status: res.status,
      headers: responseHeaders,
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
