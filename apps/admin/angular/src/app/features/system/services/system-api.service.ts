import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { SystemVersion, SystemHealth, ReinitializeResult } from '@core/models/system.model';

@Injectable({ providedIn: 'root' })
export class SystemApiService {
  private api = inject(ApiService);

  getVersion(): Observable<SystemVersion> {
    return this.api.getOne<SystemVersion>('/api/system/version');
  }

  getHealth(): Observable<SystemHealth> {
    return this.api.getOne<SystemHealth>('/api/system/health');
  }

  reinitializeAsView(): Observable<ReinitializeResult> {
    return this.api.post<Record<string, never>, ReinitializeResult>(
      '/api/system/reinitialize-asview',
      {}
    );
  }
}
