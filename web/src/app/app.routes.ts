import { Routes } from '@angular/router';

import { ChargeDetail } from './charges/charge-detail/charge-detail';
import { ChargesList } from './charges/charges-list/charges-list';

export const routes: Routes = [
  { path: '', redirectTo: 'charges', pathMatch: 'full' },
  { path: 'charges', component: ChargesList },
  { path: 'charges/:id', component: ChargeDetail },
  { path: '**', redirectTo: 'charges' },
];
