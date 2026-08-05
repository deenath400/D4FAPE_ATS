import { auth } from "@/lib/auth";

export type InvokeBackendOptions = {
  path: string;
  init?: RequestInit;
};

export class BackendInvokeError extends Error {
  constructor(
    public status: number,
    public path: string,
  ) {
    super(`Backend request to '${path}' failed with status ${status}.`);
    this.name = "BackendInvokeError";
  }
}

export function getBackendBaseUrl(): string {
  const baseUrl = process.env.API_BASE_URL;
  if (!baseUrl) {
    throw new Error("Missing required configuration key 'API_BASE_URL'.");
  }
  return baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
}

export async function invokeBackend<T>(options: InvokeBackendOptions): Promise<T> {
  const cleanBase = getBackendBaseUrl();
  const cleanPath = options.path.startsWith("/") ? options.path : `/${options.path}`;
  const url = `${cleanBase}${cleanPath}`;

  const session = await auth();
  const headers = new Headers(options.init?.headers);

  if (session?.accessToken && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  let res = await fetch(url, {
    ...options.init,
    headers,
    cache: "no-store",
  });

  if (res.status === 401 && session?.refreshToken) {
    try {
      const refreshRes = await fetch(`${cleanBase}/api/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      });

      if (refreshRes.ok) {
        const refreshedData = await refreshRes.json();
        headers.set("Authorization", `Bearer ${refreshedData.accessToken}`);
        res = await fetch(url, {
          ...options.init,
          headers,
          cache: "no-store",
        });
      }
    } catch {
      // Re-throw original 401 error if refresh fails
    }
  }

  if (!res.ok) {
    throw new BackendInvokeError(res.status, options.path);
  }

  return (await res.json()) as T;
}
