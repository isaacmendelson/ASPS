import { Injectable, inject, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { KeycloakService } from 'keycloak-angular';
import { RuntimeConfigService } from './runtime-config.service';
import { AlertBadgeService } from './alert-badge.service';
import { NotificationService } from './notification.service';

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private keycloak = inject(KeycloakService);
  private config = inject(RuntimeConfigService);
  private alertBadge = inject(AlertBadgeService);
  private notification = inject(NotificationService);

  private readonly _connected = signal(false);
  readonly connected = this._connected.asReadonly();

  async start(): Promise<void> {
    const hubUrl = `${this.config.apiUrl}/notificationshub`;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.keycloak.getToken(),
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('ReceiveNotification', (data: { Message: string }) => {
      if (data.Message === 'DeviceAlert') {
        this.alertBadge.increment();
      }
      this.notification.info('Notification', data.Message);
    });

    this.connection.onreconnected(() => this._connected.set(true));
    this.connection.onclose(() => this._connected.set(false));

    try {
      await this.connection.start();
      this._connected.set(true);
    } catch (err) {
      console.error('SignalR connection failed:', err);
      this._connected.set(false);
    }
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this._connected.set(false);
    }
  }
}
