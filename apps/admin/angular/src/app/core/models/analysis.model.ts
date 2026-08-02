import { Key } from './key.model';
import { AnalysisStatus, Severity } from './enums';

export interface AnalysisResult {
  key: Key;
  deviceKey: Key;
  userKey: Key;
  url: string;
  status: AnalysisStatus;
  severity?: Severity;
  summary?: string;
  dateCreated: string;
  dateCompleted?: string;
}
