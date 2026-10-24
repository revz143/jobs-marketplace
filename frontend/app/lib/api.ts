const apiRoot = "/backend/api/v1";

export class ApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
    this.name = "ApiError";
  }
}

const csrfToken = () =>
  document.cookie
    .split("; ")
    .find((value) => value.startsWith("XSRF-TOKEN="))
    ?.split("=")[1] ?? "";

async function ensureCsrf() {
  await fetch(`${apiRoot}/auth/csrf`, { credentials: "include" });
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = init.method ?? "GET";
  if (method !== "GET") await ensureCsrf();

  const response = await fetch(`${apiRoot}${path}`, {
    ...init,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(method !== "GET" ? { "X-XSRF-TOKEN": decodeURIComponent(csrfToken()) } : {}),
      ...(init.headers ?? {}),
    },
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => ({ title: response.statusText }));
    throw new ApiError(payload.title ?? response.statusText, response.status);
  }

  return (response.status === 204 ? null : await response.json()) as T;
}
