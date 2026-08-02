import { Key } from './key.model';
import { Severity } from './enums';

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
