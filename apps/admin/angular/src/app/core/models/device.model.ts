import { Key } from './key.model';
import { DeviceType, MonitoringStatus, OperatingSystemType } from './enums';

/** Matches the backend DeviceDto returned by GET /api/devices */
export interface DeviceDto {
  key: Key;
  deviceUid: string;
  deviceType: DeviceType;
  operatingSystem: OperatingSystemType;
  monitoringStatus: MonitoringStatus;
  make?: string;
  model?: string;
  mac?: string;
  dateCreated: string;
  userKey: Key;
  userName: string;
}

/**
 * Legacy Device shape — kept for UserDetailComponent compatibility
 * (user detail calls GET /api/users/:key/devices which returns Device entities).
 */
export interface Device {
  key: Key;
  userKey: Key;
  name: string;
  deviceType: DeviceType;
  monitoringStatus: MonitoringStatus;
  operatingSystem: OperatingSystemType;
  agentVersion?: string;
  dateRegistered: string;
  lastSeen?: string;
}

export interface DeviceWithUser extends Device {
  userFirstName: string;
  userLastName: string;
  userEmail: string;
}

export interface CreateUserDeviceRequest {
  userKeyType: string;
  userKeyValue: string;
  name: string;
  deviceType: DeviceType;
  operatingSystem: OperatingSystemType;
}

export interface UpdateDeviceRequest {
  name: string;
  monitoringStatus: MonitoringStatus;
}
