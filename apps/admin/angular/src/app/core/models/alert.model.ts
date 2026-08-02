import { Key } from './key.model';
import { Priority, Severity } from './enums';

/** Matches the backend AlertDto returned by GET /api/alerts */
export interface AlertDto {
  key: Key;
  alertType: string;
  priority: Priority;
  status: string;
  timestamp: string;
  deviceUid: string;
  deviceType: string;
  operatingSystem: string;
  deviceKey: Key;
  userKey: Key;
  userName: string;
  analysisKey?: Key;
}

/**
 * Legacy DeviceAlert shape — kept for UserDetailComponent compatibility
 * (user detail calls GET /api/users/:key/alerts which returns DeviceAlert entities).
 */
export interface DeviceAlert {
  key: Key;
  deviceKey: Key;
  userKey: Key;
  title: string;
  description: string;
  severity: Severity;
  url?: string;
  dateCreated: string;
  dateRead?: string;
  isRead: boolean;
}
