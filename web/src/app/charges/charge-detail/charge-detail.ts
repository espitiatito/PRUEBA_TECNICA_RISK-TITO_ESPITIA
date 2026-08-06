import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import {
  ChargeAttempt,
  ChargeDetail as ChargeDetailDto,
  ETIQUETAS_ESTADO,
  ETIQUETAS_MOTIVO,
} from '../../models/charge';
import { ChargesService } from '../../services/charges.service';

@Component({
  selector: 'app-charge-detail',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './charge-detail.html',
})
export class ChargeDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly charges = inject(ChargesService);

  id = '';
  cobro = signal<ChargeDetailDto | null>(null);
  intentos = signal<ChargeAttempt[]>([]);
  mensaje = signal('');

  readonly etiquetasEstado = ETIQUETAS_ESTADO;
  readonly etiquetasMotivo = ETIQUETAS_MOTIVO;

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.cargar();
  }

  cargar(): void {
    this.charges.get(this.id).subscribe((cobro) => {
      this.cobro.set(cobro);

      // el historial nos llegaba desordenado, lo pedimos aparte
      this.charges.get(this.id).subscribe((detalle) => {
        this.intentos.set(detalle.attempts);
      });
    });
  }

  reintentar(): void {
    const cobro = this.cobro();
    if (!cobro) {
      return;
    }

    this.mensaje.set('');
    this.charges.retry(cobro.id, cobro.amount).subscribe((respuesta) => {
      this.mensaje.set(respuesta.message);
      this.cargar();
    });
  }
}
