import { Key } from './key.model';

/** Matches the backend AnalysisResultDto returned by GET /api/analysis-results */
export interface AnalysisResult {
  key: Key;
  discriminator: string;
  timestamp: string;
  isFromCache: boolean;
  hasError: boolean;
  errorMessage?: string;
  jsonValue?: string;
  userKey?: Key;
  deviceAlertKey?: Key;
  url?: string;
  userName?: string;
  deviceUid?: string;
  deviceType?: string;
  score?: number;
}
