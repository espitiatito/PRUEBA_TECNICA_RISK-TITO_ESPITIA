import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, Subscription, debounceTime, distinctUntilChanged } from 'rxjs';

import {
  Charge,
  ChargeFilters,
  ESTADOS,
  ETIQUETAS_ESTADO,
  ETIQUETAS_MOTIVO,
  MOTIVOS,
} from '../../models/charge';
import { AppError } from '../../interceptors/error.interceptor';
import { ChargesService } from '../../services/charges.service';

export type ViewState = 'cargando' | 'vacio' | 'error' | 'datos';

@Component({
  selector: 'app-charges-list',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe],
  templateUrl: './charges-list.html',
})
export class ChargesList implements OnInit, OnDestroy {
  private readonly chargesService = inject(ChargesService);
  private readonly searchSubject = new Subject<string>();
  private searchSub?: Subscription;
  private listSub?: Subscription;

  // Estados de vista explícitos (RF-1 / Frontend guidelines)
  readonly estadoVista = signal<ViewState>('cargando');
  readonly cobros = signal<Charge[]>([]);
  readonly totalRegistros = signal<number>(0);
  readonly totalPaginas = signal<number>(1);
  readonly reintentandoId = signal<string | null>(null);

  // Mensajes de retroalimentación diferenciados
  readonly mensajeExito = signal<string>('');
  readonly mensajeReglaNegocio = signal<string>('');
  readonly mensajeErrorSistema = signal<string>('');

  // Filtros reactivos
  estado = 'failed';
  motivo = '';
  desde = '';
  hasta = '';
  texto = '';

  // Paginación y ordenamiento en servidor (RF-1)
  pagina = 1;
  tamanoPagina = 10;
  sortBy = 'date';
  sortDir: 'asc' | 'desc' = 'desc';

  readonly estados = ESTADOS;
  readonly motivos = MOTIVOS;
  readonly etiquetasEstado = ETIQUETAS_ESTADO;
  readonly etiquetasMotivo = ETIQUETAS_MOTIVO;

  ngOnInit(): void {
    this.searchSub = this.searchSubject
      .pipe(
        debounceTime(350),
        distinctUntilChanged()
      )
      .subscribe(() => {
        this.pagina = 1;
        this.cargarCobros();
      });

    this.cargarCobros();
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
    this.listSub?.unsubscribe();
  }

  onSearchChange(): void {
    this.searchSubject.next(this.texto);
  }

  filtrar(): void {
    this.pagina = 1;
    this.cargarCobros();
  }

  cambiarOrden(campo: string): void {
    if (this.sortBy === campo) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = campo;
      this.sortDir = 'desc';
    }
    this.pagina = 1;
    this.cargarCobros();
  }

  irA(nuevaPagina: number): void {
    if (nuevaPagina < 1 || nuevaPagina > this.totalPaginas() || nuevaPagina === this.pagina) {
      return;
    }
    this.pagina = nuevaPagina;
    this.cargarCobros();
  }

  cargarCobros(): void {
    // Cancelamos cualquier petición previa en vuelo para evitar que una respuesta lenta sobreescriba una nueva
    this.listSub?.unsubscribe();

    this.estadoVista.set('cargando');
    this.limpiarMensajes();

    const filters: ChargeFilters = {
      status: this.estado || undefined,
      failureReason: this.motivo || undefined,
      search: this.texto.trim() || undefined,
      page: this.pagina,
      pageSize: this.tamanoPagina,
      sortBy: this.sortBy,
      sortDir: this.sortDir,
    };

    // Manejo de criterio de zona horaria UTC limpio
    if (this.desde) {
      filters.from = `${this.desde}T00:00:00Z`;
    }
    if (this.hasta) {
      filters.to = `${this.hasta}T23:59:59Z`;
    }

    this.listSub = this.chargesService.list(filters).subscribe({
      next: (resultado) => {
        this.cobros.set(resultado.items);
        this.totalRegistros.set(resultado.totalCount);
        this.totalPaginas.set(resultado.totalPages);

        if (resultado.items.length === 0) {
          this.estadoVista.set('vacio');
        } else {
          this.estadoVista.set('datos');
        }
      },
      error: (err: AppError) => {
        this.mensajeErrorSistema.set(
          err.message || 'Error al comunicarse con el servicio de cobros.'
        );
        this.estadoVista.set('error');
      },
    });
  }

  reintentar(cobro: Charge): void {
    if (this.reintentandoId()) {
      return; // Bloqueo preventivo en frontend
    }

    this.limpiarMensajes();
    this.reintentandoId.set(cobro.id);

    this.chargesService.retry(cobro.id, cobro.amount).subscribe({
      next: (respuesta) => {
        this.reintentandoId.set(null);
        this.mensajeExito.set(
          respuesta.message || 'Cobro procesado correctamente.'
        );
        this.cargarCobros();
      },
      error: (err: AppError) => {
        this.reintentandoId.set(null);

        // Diferenciación visual exigida en RF-4:
        // "No puedes reintentar esto y este es el motivo" vs "Algo se rompió"
        if (err.isBusinessRule) {
          this.mensajeReglaNegocio.set(
            `No es posible reintentar este cobro: ${err.message}`
          );
        } else {
          this.mensajeErrorSistema.set(
            `Fallo técnico en la operación: ${err.message}`
          );
        }
        this.cargarCobros();
      },
    });
  }

  puedeReintentar(cobro: Charge): boolean {
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
