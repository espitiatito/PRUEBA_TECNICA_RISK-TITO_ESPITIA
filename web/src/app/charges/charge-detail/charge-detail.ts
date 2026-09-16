import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';

import {
  ChargeAttempt,
  ChargeDetail as ChargeDetailDto,
  ETIQUETAS_ESTADO,
  ETIQUETAS_MOTIVO,
} from '../../models/charge';
import { AppError } from '../../interceptors/error.interceptor';
import { ChargesService } from '../../services/charges.service';

@Component({
  selector: 'app-charge-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './charge-detail.html',
})
export class ChargeDetail implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly chargesService = inject(ChargesService);
  private detailSub?: Subscription;

  id = '';
  readonly cobro = signal<ChargeDetailDto | null>(null);
  readonly intentos = signal<ChargeAttempt[]>([]);
  readonly cargando = signal<boolean>(true);
  readonly errorCarga = signal<string>('');
  readonly reintentando = signal<boolean>(false);

  readonly mensajeExito = signal<string>('');
  readonly mensajeReglaNegocio = signal<string>('');
  readonly mensajeErrorSistema = signal<string>('');

  readonly etiquetasEstado = ETIQUETAS_ESTADO;
  readonly etiquetasMotivo = ETIQUETAS_MOTIVO;

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.cargar();
  }

  ngOnDestroy(): void {
    this.detailSub?.unsubscribe();
  }

  cargar(): void {
    this.detailSub?.unsubscribe();
    this.cargando.set(true);
    this.errorCarga.set('');

    // Corrección del bug base: Una única llamada HTTP que ya trae el historial ordenado
    this.detailSub = this.chargesService.get(this.id).subscribe({
      next: (detalle) => {
        this.cobro.set(detalle);
        this.intentos.set(detalle.attempts ?? []);
        this.cargando.set(false);
      },
      error: (err: AppError) => {
        this.errorCarga.set(err.message || 'No se pudo cargar el detalle del cobro.');
        this.cargando.set(false);
      },
    });
  }

  reintentar(): void {
    const cobro = this.cobro();
    if (!cobro || this.reintentando()) {
      return;
    }

    this.limpiarMensajes();
    this.reintentando.set(true);

    this.chargesService.retry(cobro.id, cobro.amount).subscribe({
      next: (respuesta) => {
        this.reintentando.set(false);
        this.mensajeExito.set(respuesta.message || 'Cobro reintentado con éxito.');
        this.cargar();
      },
      error: (err: AppError) => {
        this.reintentando.set(false);
        if (err.isBusinessRule) {
          this.mensajeReglaNegocio.set(`No es posible reintentar este cobro: ${err.message}`);
        } else {
          this.mensajeErrorSistema.set(`Error técnico al procesar el reintento: ${err.message}`);
        }
        this.cargar();
      },
    });
  }

  puedeReintentar(): boolean {
    const cobro = this.cobro();
    if (!cobro) return false;

    return (
      cobro.status !== 'paid' &&
      cobro.status !== 'canceled' &&
      cobro.status !== 'processing' &&
      cobro.attemptCount < 3 &&
      cobro.failureReason !== 'tarjeta_vencida'
    );
  }

  private limpiarMensajes(): void {
    this.mensajeExito.set('');
    this.mensajeReglaNegocio.set('');
    this.mensajeErrorSistema.set('');
  }
}
