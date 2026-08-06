import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';

import { ESTADOS, ETIQUETAS_ESTADO, ETIQUETAS_MOTIVO, MOTIVOS } from '../../models/charge';

@Component({
  selector: 'app-charges-list',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe],
  templateUrl: './charges-list.html',
})
export class ChargesList implements OnInit {
  private readonly http = inject(HttpClient);

  cobros = signal<any[]>([]);
  mensaje = signal('');

  estado = 'failed';
  motivo = '';
  desde = '';
  hasta = '';
  texto = '';

  pagina = 1;
  tamanoPagina = 10;

  readonly estados = ESTADOS;
  readonly motivos = MOTIVOS;
  readonly etiquetasEstado = ETIQUETAS_ESTADO;
  readonly etiquetasMotivo = ETIQUETAS_MOTIVO;

  ngOnInit(): void {
    let url = 'http://localhost:5080/api/charges?page=1&pageSize=500';

    if (this.estado) {
      url += '&status=' + this.estado;
    }
    if (this.motivo) {
      url += '&failureReason=' + this.motivo;
    }
    if (this.desde) {
      url += '&from=' + new Date(this.desde + 'T00:00:00').toISOString();
    }
    if (this.hasta) {
      url += '&to=' + new Date(this.hasta + 'T23:59:59').toISOString();
    }

    this.http
      .get<any>(url)
      .pipe(catchError(() => of([])))
      .subscribe((data: any) => {
        this.cobros.set(data);
      });
  }

  // TODO: paginar esto bien, por ahora se corta en el cliente
  get filas(): any[] {
    const inicio = (this.pagina - 1) * this.tamanoPagina;
    return this.filtrados().slice(inicio, inicio + this.tamanoPagina);
  }

  get totalPaginas(): number {
    return Math.max(1, Math.ceil(this.filtrados().length / this.tamanoPagina));
  }

  filtrar(): void {
    this.pagina = 1;
    this.ngOnInit();
  }

  irA(pagina: number): void {
    if (pagina < 1 || pagina > this.totalPaginas) {
      return;
    }
    this.pagina = pagina;
  }

  reintentar(cobro: any): void {
    this.mensaje.set('');
    this.cobros.update((lista: any[]) =>
      lista.map((c: any) => (c.id === cobro.id ? { ...c, status: 'paid' } : c)),
    );

    this.http
      .post<any>('http://localhost:5080/api/charges/' + cobro.id + '/retry', { amount: cobro.amount })
      .subscribe((respuesta: any) => {
        this.mensaje.set(respuesta.message);
        if (respuesta.success) {
          this.ngOnInit();
        }
      });
  }

  private filtrados(): any[] {
    const lista = this.cobros();
    if (!this.texto) {
      return lista;
    }

    const buscado = this.texto.toLowerCase();
    return lista.filter(
      (c: any) =>
        c.customerName.toLowerCase().includes(buscado) ||
        c.externalReference.toLowerCase().includes(buscado),
    );
  }
}
