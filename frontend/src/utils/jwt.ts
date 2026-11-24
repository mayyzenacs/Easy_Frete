import type {
  RoleType,
  StatusType,
  UserDisplayMetadata,
  UserProfile,
} from "../types";
import { normalizarRole, normalizarStatus } from "./formatters";

const CLAIM_NAME_ID =
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
const CLAIM_NAME =
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
const CLAIM_EMAIL =
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";
const CLAIM_ROLE =
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

export interface JwtPayload {
  sub?: unknown;
  id?: unknown;
  nameid?: unknown;
  unique_name?: unknown;
  name?: unknown;
  email?: unknown;
  role?: unknown;
  status?: unknown;
  exp?: unknown;
  [key: string]: unknown;
}

const firstNonEmptyString = (...values: unknown[]): string => {
  for (const value of values) {
    if (typeof value === "string" && value.trim().length > 0) {
      return value.trim();
    }
  }
  return "";
};

const extractRoleFromClaim = (rawRole: unknown): RoleType => {
  if (Array.isArray(rawRole)) {
    const isAdmin = rawRole.some(
      (entry) => String(entry).toLowerCase() === "admin",
    );
    return isAdmin ? "Admin" : "User";
  }
  return normalizarRole(rawRole);
};

const extractStatusFromClaim = (rawStatus: unknown): StatusType => {
  if (typeof rawStatus === "string" && rawStatus.trim().length > 0) {
    return normalizarStatus(rawStatus);
  }
  // A API emite JWT exclusivamente para contas com status "Aprovado".
  return "Aprovado";
};

export const decodeJwtPayload = (token: string): JwtPayload | null => {
  const parts = token.split(".");
  if (parts.length !== 3 || !parts[1]) return null;

  try {
    const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(
      base64.length + ((4 - (base64.length % 4)) % 4),
      "=",
    );
    const binary = atob(padded);
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
    const json = new TextDecoder().decode(bytes);
    const parsed: unknown = JSON.parse(json);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return null;
    }
    return parsed as JwtPayload;
  } catch {
    return null;
  }
};

export const isJwtExpired = (
  payload: JwtPayload,
  nowMs: number = Date.now(),
): boolean => {
  if (typeof payload.exp !== "number" || !Number.isFinite(payload.exp)) {
    return true;
  }
  return payload.exp * 1000 <= nowMs;
};

export const parseUserFromJwt = (
  token: string,
  fallbackDisplay?: Partial<UserDisplayMetadata>,
  nowMs: number = Date.now(),
): UserProfile | null => {
  const payload = decodeJwtPayload(token);
  if (!payload || isJwtExpired(payload, nowMs)) {
    return null;
  }

  const id = firstNonEmptyString(
    payload.sub,
    payload.id,
    payload.nameid,
    payload[CLAIM_NAME_ID],
  );
  if (!id) return null;

  const nome =
    firstNonEmptyString(
      payload.unique_name,
      payload.name,
      payload[CLAIM_NAME],
    ) ||
    fallbackDisplay?.nome?.trim() ||
    "";

  const email =
    firstNonEmptyString(payload.email, payload[CLAIM_EMAIL]) ||
    fallbackDisplay?.email?.trim() ||
    "";

  const role = extractRoleFromClaim(payload.role ?? payload[CLAIM_ROLE]);
  const status = extractStatusFromClaim(payload.status);

  return { id, nome, email, role, status };
};
