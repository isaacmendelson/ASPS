import { Key } from './key.model';
import { DeviceType, MonitoringStatus, OperatingSystemType } from './enums';

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
