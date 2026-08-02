import { Key } from './key.model';
import { UserRole } from './enums';

export interface UserAccount {
  id: string;
  accountType: string;
  accountValue: string;
}

export interface UserDevice {
  key: Key;
  name: string;
  deviceType: string;
  monitoringStatus: string;
  operatingSystem: string;
  dateRegistered: string;
}

export interface User {
  key: Key;
  keycloakUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  dateCreated: string;  // ISO 8601
  city?: string;
  state?: string;
  phoneNumber?: string;
}

export interface UserWithDeviceCount extends User {
  deviceCount: number;
}

export interface UserDetails extends User {
  address?: string;
  city?: string;
  state?: string;
  zip?: string;
  country?: string;
  phoneNumber?: string;
  locale?: string;
  timezone?: string;
  devices: UserDevice[];
  accounts: UserAccount[];
}

export interface CreateUserRequest {
  keycloakUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  address?: string;
  city?: string;
  state?: string;
  zip?: string;
  country?: string;
  locale?: string;
  timezone?: string;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  address: string;
  city: string;
  phoneNumber: string;
}
