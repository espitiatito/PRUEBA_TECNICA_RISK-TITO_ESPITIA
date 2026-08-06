import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { Charge, ChargeDetail, ChargeFilters, RetryResult } from '../models/charge';

@Injectable({ providedIn: 'root' })
export class ChargesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/charges`;

  list(filters: ChargeFilters = {}): Observable<Charge[]> {
    let params = new HttpParams();

    if (filters.status) {
      params = params.set('status', filters.status);
    }
    if (filters.failureReason) {
      params = params.set('failureReason', filters.failureReason);
    }
    if (filters.from) {
      params = params.set('from', filters.from);
    }
    if (filters.to) {
      params = params.set('to', filters.to);
    }
    if (filters.page) {
      params = params.set('page', filters.page);
    }
    if (filters.pageSize) {
      params = params.set('pageSize', filters.pageSize);
    }

    return this.http.get<Charge[]>(this.baseUrl, { params });
  }

  get(id: string): Observable<ChargeDetail> {
    return this.http.get<ChargeDetail>(`${this.baseUrl}/${id}`);
  }

  retry(id: string, amount: number): Observable<RetryResult> {
    return this.http.post<RetryResult>(`${this.baseUrl}/${id}/retry`, {
      amount,
      triggeredBy: 'operaciones',
    });
  }
}
