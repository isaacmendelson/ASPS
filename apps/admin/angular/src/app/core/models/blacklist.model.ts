// Mirrors backend DTOs in WebApi.Controllers.Api.Blacklists.BlacklistDtos
// Updated for ASPS-654 to match actual backend shapes from ASPS-648.

export interface PhishingWebsite {
  id: number;
  url: string;
  domain: string;
  source?: string;
  dateCreated: string;
  isActive: boolean;
}

export interface BlacklistedPhoneNumber {
  id: number;
  phoneNumber: string;
  source?: string;
  notes?: string;
  dateCreated: string;
  isDeleted: boolean;
}

export interface BankWebsite {
  id: number;
  domain: string;
  bankName: string;
  country?: string;
  isActive: boolean;
  dateCreated: string;
  dateModified?: string;
}

export interface WebsiteCategory {
  key: string;
  name: string;
  parentId?: string;
  parentName?: string;
  source?: string;
  dateCreated: string;
  isDeleted: boolean;
}

export interface CreateWebsiteCategoryRequest {
  name: string;
  parentId?: string;
  source?: string;
}

export interface UpdateWebsiteCategoryRequest {
  parentId?: string;
  source?: string;
}

export interface TrackedDomain {
  id: number;
  domain: string;
  category: string;
  userKey?: string;
  scamInProgressKey?: string;
  analysisKey?: string;
  trackMode: number;
  reason?: string;
  isActive: boolean;
  dateCreated: string;
  dateModified: string;
}

export interface CreateTrackedDomainRequest {
  domain: string;
  category: string;
  userKeyField?: string;
  scamInProgressKey?: string;
  analysisKey?: string;
  trackMode: number;
  reason?: string;
  notifyAllUsers: boolean;
}

export interface UpdateTrackedDomainRequest {
  domain?: string;
  category?: string;
  userKeyField?: string;
  scamInProgressKey?: string;
  trackMode: number;
  isActive: boolean;
  reason?: string;
}

export interface NotifyUserRequest {
  userKeyField: string;
  reason?: string;
}
