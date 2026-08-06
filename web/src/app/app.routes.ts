import { Routes } from '@angular/router';

import { ChargesList } from './charges/charges-list/charges-list';

export const routes: Routes = [
  { path: '', redirectTo: 'charges', pathMatch: 'full' },
  { path: 'charges', component: ChargesList },
];
