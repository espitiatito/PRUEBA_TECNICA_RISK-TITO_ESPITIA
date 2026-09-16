import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { Charge, ChargeDetail, ChargeFilters, PagedResult, RetryResult } from '../models/charge';

@Injectable({ providedIn: 'root' })
export class ChargesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/charges`;

  list(filters: ChargeFilters = {}): Observable<PagedResult<Charge>> {
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
    if (filters.search) {
      params = params.set('search', filters.search);
    }
    if (filters.page) {
      params = params.set('page', filters.page.toString());
    }
    if (filters.pageSize) {
      params = params.set('pageSize', filters.pageSize.toString());
    }
    if (filters.sortBy) {
      params = params.set('sortBy', filters.sortBy);
    }
    if (filters.sortDir) {
      params = params.set('sortDir', filters.sortDir);
    }

    return this.http.get<PagedResult<Charge>>(this.baseUrl, { params });
  }

  get(id: string): Observable<ChargeDetail> {
    return this.http.get<ChargeDetail>(`${this.baseUrl}/${id}`);
  }

  retry(id: string, amount: number): Observable<RetryResult> {
    return this.http.post<RetryResult>(`${this.baseUrl}/${id}/retry`, {
      amount,
      triggeredBy: 'ops@empresa.com',
    });
  }
}
