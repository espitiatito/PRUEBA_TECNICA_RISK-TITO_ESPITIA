export interface Charge {
  id: string;
  customerName: string;
  customerEmail: string;
  externalReference: string;
  amount: number;
  currency: string;
  dueDate: string;
  status: string;
  failureReason: string | null;
  attemptCount: number;
}

export interface ChargeAttempt {
  id: string;
  attemptedAt: string;
  triggeredBy: string;
  succeeded: boolean;
  gatewayMessage: string | null;
  gatewayReference: string | null;
}

export interface ChargeDetail extends Charge {
  customerId: string;
  subscriptionId: string;
  createdAt: string;
  attempts: ChargeAttempt[];
}

export interface ChargeFilters {
  status?: string;
  failureReason?: string;
  from?: string;
  to?: string;
  search?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface RetryResult {
  success: boolean;
  message: string;
  errorCode?: string | null;
  data?: Charge | null;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errorCode?: string;
  [key: string]: unknown;
}

export const ESTADOS = ['pending', 'failed', 'paid', 'canceled', 'processing'];

export const MOTIVOS = [
  'tarjeta_rechazada',
  'fondos_insuficientes',
  'tarjeta_vencida',
  'timeout_pasarela',
  'error_interno',
];

export const ETIQUETAS_ESTADO: Record<string, string> = {
  pending: 'Pendiente',
  failed: 'Fallido',
  paid: 'Pagado',
  canceled: 'Anulado',
  processing: 'En proceso',
};

export const ETIQUETAS_MOTIVO: Record<string, string> = {
  tarjeta_rechazada: 'Tarjeta rechazada',
  fondos_insuficientes: 'Fondos insuficientes',
  tarjeta_vencida: 'Tarjeta vencida',
  timeout_pasarela: 'Timeout de la pasarela',
  error_interno: 'Error interno',
};
