import type {
  DeletionRequestStatus,
  RoleType,
  StatusType,
} from "../types";

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[A-Za-z]{2,}$/;

export const isValidEmail = (valor: string): boolean =>
  EMAIL_REGEX.test(valor.trim());

export const brl = (v: number): string =>
  new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(
    Number.isFinite(v) ? v : 0,
  );

export const fmtDecimalBr = (v: number, decimals = 2): string =>
  (Number.isFinite(v) ? v : 0).toFixed(decimals).replace(".", ",");

export const fmtPercentBr = (v: number): string => {
  if (!Number.isFinite(v)) return "0";
  return Number.isInteger(v)
    ? String(v)
    : Number(v.toFixed(2)).toString().replace(".", ",");
};

export const sanitizeDecimalInput = (raw: string): string => {
  let clean = String(raw ?? "").replace(/[^\d.,]/g, "");
  if (!clean) return "";

  const lastComma = clean.lastIndexOf(",");
  const lastDot = clean.lastIndexOf(".");

  // Quando há ponto e vírgula (ex.: "1.345,90" ou "1,345.90"), o último é o separador decimal
  if (lastComma !== -1 && lastDot !== -1) {
    clean =
      lastComma > lastDot ? clean.replace(/\./g, "") : clean.replace(/,/g, "");
  }

  const sepMatches = clean.match(/[.,]/g);
  if (sepMatches && sepMatches.length > 1) {
    const sep = clean.includes(",") ? "," : ".";
    const parts = clean.split(sep);
    const lastPart = parts[parts.length - 1];
    // Trata múltiplos separadores de milhar colados (ex.: "1.200.000")
    if (lastPart.length === 3 && parts.slice(1).every((p) => p.length === 3)) {
      clean = parts.join("");
    } else {
      clean = parts[0] + sep + parts.slice(1).join("");
    }
  }

  return clean;
};

export const num = (s: string): number => {
  const n = parseFloat(sanitizeDecimalInput(s).replace(",", "."));
  return Number.isFinite(n) && n >= 0 ? n : 0;
};

export const fmtKg = (v: number): string => v.toFixed(3).replace(".", ",");

export const formatarDataSolicitacao = (iso?: string | null): string => {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  const data = d.toLocaleDateString("pt-BR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
  const hora = d.toLocaleTimeString("pt-BR", {
    hour: "2-digit",
    minute: "2-digit",
  });
  return `${data} às ${hora}`;
};

export const normalizarRole = (role: unknown): RoleType => {
  const r = String(role).toLowerCase();
  if (r === "master") return "Master";
  if (r === "admin") return "Admin";
  return "User";
};

export const isAdminOrMaster = (role?: RoleType): boolean =>
  role === "Master" || role === "Admin";

export const normalizarStatus = (status: unknown): StatusType => {
  const s = String(status).toLowerCase();
  if (s === "aprovado") return "Aprovado";
  if (s === "rejeitado") return "Rejeitado";
  return "Pendente";
};

export const normalizarDeletionStatus = (
  status: unknown,
): DeletionRequestStatus => {
  const s = String(status).toLowerCase();
  if (s === "aprovado") return "Aprovado";
  if (s === "recusado") return "Recusado";
  return "Pendente";
};

export const labelRole = (role?: RoleType): string => {
  if (role === "Master") return "Master";
  if (role === "Admin") return "Administrador";
  return "Usuário";
};

export const statusBadgeClass: Record<StatusType, string> = {
  Aprovado: "bg-emerald-500/10 text-emerald-300 border-emerald-500/25",
  Pendente: "bg-yellow-400/10 text-yellow-300 border-yellow-400/25",
  Rejeitado: "bg-red-500/10 text-red-300 border-red-500/25",
};

export const statusTextClass: Record<StatusType, string> = {
  Aprovado: "text-emerald-400",
  Pendente: "text-yellow-400",
  Rejeitado: "text-red-400",
};
