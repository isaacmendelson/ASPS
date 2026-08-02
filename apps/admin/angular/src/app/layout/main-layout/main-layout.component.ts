import {
  Component,
  OnDestroy,
  OnInit,
  inject,
  signal,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { MatSidenavModule, MatSidenav } from '@angular/material/sidenav';
import { BreakpointObserver } from '@angular/cdk/layout';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs/operators';

import { SidebarComponent } from '../sidebar/sidebar.component';
import { TopbarComponent } from '../topbar/topbar.component';
import { SignalRService } from '../../core/services/signalr.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    MatSidenavModule,
    SidebarComponent,
    TopbarComponent,
  ],
  templateUrl: './main-layout.component.html',
})
export class MainLayoutComponent implements OnInit, OnDestroy {
  @ViewChild('sidenav') sidenav!: MatSidenav;

  private breakpointObserver = inject(BreakpointObserver);
  private signalR = inject(SignalRService);

  isDesktop = toSignal(
    this.breakpointObserver
      .observe('(min-width: 1024px)')
      .pipe(map(result => result.matches)),
    { initialValue: true }
  );

  async ngOnInit(): Promise<void> {
    // Start SignalR for real-time notifications.
    // If Keycloak isn't configured the connection will fail silently.
    await this.signalR.start();
  }

  async ngOnDestroy(): Promise<void> {
    await this.signalR.stop();
  }

  toggleSidenav(): void {
    this.sidenav?.toggle();
  }
}
