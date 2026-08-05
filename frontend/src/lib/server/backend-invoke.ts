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

// TOKEN-ATTACHMENT-POINT (0002): attach an Authorization header here once a NextAuth
// session is available. This spec sends none (FR-16, AC-27).
export async function invokeBackend<T>(options: InvokeBackendOptions): Promise<T> {
  const baseUrl = process.env.API_BASE_URL;
  if (!baseUrl) {
    throw new Error("Missing required configuration key 'API_BASE_URL'.");
  }

  const cleanBase = baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
  const cleanPath = options.path.startsWith("/") ? options.path : `/${options.path}`;
  const url = `${cleanBase}${cleanPath}`;

  const res = await fetch(url, {
    ...options.init,
    cache: "no-store",
  });

  if (!res.ok) {
    throw new BackendInvokeError(res.status, options.path);
  }

  return (await res.json()) as T;
}
