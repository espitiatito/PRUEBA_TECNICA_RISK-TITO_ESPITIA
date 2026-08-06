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
  page?: number;
  pageSize?: number;
}

export interface RetryResult {
  success: boolean;
  message: string;
  data?: Charge | null;
}

export const ESTADOS = ['pending', 'failed', 'paid', 'canceled'];

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
};

export const ETIQUETAS_MOTIVO: Record<string, string> = {
  tarjeta_rechazada: 'Tarjeta rechazada',
  fondos_insuficientes: 'Fondos insuficientes',
  tarjeta_vencida: 'Tarjeta vencida',
  timeout_pasarela: 'Timeout de la pasarela',
  error_interno: 'Error interno',
};
