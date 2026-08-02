/** Mirrors GET /api/system/version response. */
export interface SystemVersion {
  version: string;
  buildDate?: string;
  gitCommitId?: string;
  isPrerelease?: boolean;
  isPublicRelease?: boolean;
}

/** Mirrors GET /api/system/health response. */
export interface SystemHealth {
  status: string;
  timestamp: string;
}

/** Mirrors POST /api/system/reinitialize-asview response. */
export interface ReinitializeResult {
  message: string;
}
