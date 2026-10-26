import type { FetchBaseQueryError } from "@reduxjs/toolkit/query";

export interface FieldError {
  field: string | null;
  code: string;
  message: string;
}

/** RFC 9457 Problem Details from the API, plus the extensions it adds. */
export interface ApiProblem {
  status: number;
  code: string;
  title?: string;
  detail?: string;
  errors: FieldError[];
  transferId?: string;
  retryAfter?: number;
  traceId?: string;
}

/** Network failures and timeouts become a problem too, so every hook returns one error shape. */
export const NETWORK_ERROR = "network_error";

interface ProblemBody {
  status?: number;
  code?: string;
  title?: string;
  detail?: string;
  errors?: FieldError[];
  transferId?: string;
  traceId?: string;
}

export function toApiProblem(
  error: FetchBaseQueryError,
  retryAfterHeader?: string | null,
): ApiProblem {
  if (typeof error.status !== "number") {
    return {
      status: 0,
      code: NETWORK_ERROR,
      errors: [],
      detail: "error" in error ? error.error : undefined,
    };
  }
  const body = (error.data ?? {}) as ProblemBody;
  const retryAfter = retryAfterHeader ? Number(retryAfterHeader) : undefined;
  return {
    status: error.status,
    code: body.code ?? `http_${error.status}`,
    title: body.title,
    detail: body.detail,
    errors: Array.isArray(body.errors) ? body.errors : [],
    transferId: body.transferId,
    traceId: body.traceId,
    retryAfter: Number.isFinite(retryAfter) ? retryAfter : undefined,
  };
}

export function isApiProblem(value: unknown): value is ApiProblem {
  return typeof value === "object" && value !== null && "code" in value && "errors" in value;
}
