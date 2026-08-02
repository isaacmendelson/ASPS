import { Component, EventEmitter, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { KeycloakService } from 'keycloak-angular';

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
  ],
  templateUrl: './topbar.component.html',
  styleUrls: ['./topbar.component.scss'],
})
export class TopbarComponent {
  @Output() menuToggle = new EventEmitter<void>();

  private keycloak = inject(KeycloakService);
  private router = inject(Router);

  get username(): string {
    const token = this.keycloak.getKeycloakInstance().tokenParsed;
    return (
      (token?.['preferred_username'] as string | undefined) ?? 'Admin'
    );
  }

  logout(): void {
    this.keycloak.logout(window.location.origin);
  }
}
