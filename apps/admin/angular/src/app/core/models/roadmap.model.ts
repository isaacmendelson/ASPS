/** Mirrors the Roadmap entity returned by GET /api/roadmaps. */
export interface Roadmap {
  id: number;
  name: string;
  description?: string;
  /** Raw JSON string — stored as JSONB / text in backend. */
  data?: string;
  version?: number;
  isArchived: boolean;
  createdBy?: string;
  lastUpdatedBy?: string;
  dateCreated: string;
  lastUpdatedAt?: string;
}

export interface CreateRoadmapRequest {
  name: string;
  description?: string;
}
