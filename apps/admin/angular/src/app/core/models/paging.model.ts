// Mirrors Common.Models.PagedRequest and PagedResult<T> in the .NET backend.

export interface PagedRequest {
  page?: number;           // 1-based, default 1
  pageSize?: number;       // default 25, max 100
  search?: string;         // free-text filter
  sortBy?: string;         // column name
  sortDirection?: 'asc' | 'desc';  // default 'asc'
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
