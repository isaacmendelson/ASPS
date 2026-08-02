import { Key } from './key.model';
import { CautionLevel, TrackMode } from './enums';

export interface PhishingWebsite {
  key: Key;
  url: string;
  domain: string;
  cautionLevel: CautionLevel;
  dateAdded: string;
  addedBy?: string;
}

export interface BlacklistedPhoneNumber {
  key: Key;
  phoneNumber: string;
  cautionLevel: CautionLevel;
  description?: string;
  dateAdded: string;
}

export interface BankWebsite {
  key: Key;
  url: string;
  bankName: string;
  country?: string;
  dateAdded: string;
}

export interface WebsiteCategory {
  key: Key;
  name: string;
  description?: string;
  trackMode: TrackMode;
  cautionLevel: CautionLevel;
  dateCreated: string;
}

export interface TrackedDomain {
  key: Key;
  domain: string;
  categoryKey?: Key;
  cautionLevel: CautionLevel;
  dateAdded: string;
}
