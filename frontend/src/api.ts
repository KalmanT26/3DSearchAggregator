const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api';

export interface ModelDto {
  id: number;
  externalId: string;
  source: string;
  sourceUrl: string;
  title: string;
  description: string;
  tags: string[];
  thumbnailUrl: string;
  imageUrls: string[];
  creatorName: string;
  creatorProfileUrl: string;
  price: number;
  currency: string;
  isFree: boolean;
  isSubscriptionGated: boolean;

  likeCount: number;
  viewCount?: number;
  makeCount?: number;
  fileCount?: number;

  descriptionHtml?: string;
  license: string | null;
  category: string | null;
  createdAtSource: string;
}

export async function getModelDetails(source: string, id: string): Promise<ModelDto> {
  // Use encodeURIComponent to handle special characters in IDs (like slashes in URLs if any, though usually IDs are safe)
  const safeId = encodeURIComponent(id);
  const res = await fetch(`${API_BASE}/models/${source}/${safeId}`);
  if (!res.ok) throw new Error(`Failed to fetch details: ${res.status}`);
  return res.json();
}

export interface SearchResponse {
  results: ModelDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface SearchParams {
  q?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sources?: string[];
  freeOnly?: boolean;
  minPrice?: number;
  maxPrice?: number;
}

export interface FilterOptions {
  sources: string[];
  sortOptions: { value: string; label: string }[];
}

export async function searchModels(params: SearchParams): Promise<SearchResponse> {
  const searchParams = new URLSearchParams();

  if (params.q) searchParams.set('q', params.q);
  if (params.page) searchParams.set('page', params.page.toString());
  if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());
  if (params.sortBy) searchParams.set('sortBy', params.sortBy);
  if (params.sources?.length) searchParams.set('sources', params.sources.join(','));
  if (params.freeOnly) searchParams.set('freeOnly', 'true');
  if (params.minPrice != null) searchParams.set('minPrice', params.minPrice.toString());
  if (params.maxPrice != null) searchParams.set('maxPrice', params.maxPrice.toString());

  const res = await fetch(`${API_BASE}/models/search?${searchParams}`);
  if (!res.ok) throw new Error(`Search failed: ${res.status}`);
  return res.json();
}

export async function getTrending(page = 1, pageSize = 24): Promise<SearchResponse> {
  const res = await fetch(`${API_BASE}/models/trending?page=${page}&pageSize=${pageSize}`);
  if (!res.ok) throw new Error(`Trending failed: ${res.status}`);
  return res.json();
}

export async function getFilters(): Promise<FilterOptions> {
  const res = await fetch(`${API_BASE}/models/filters`);
  if (!res.ok) throw new Error(`Filters failed: ${res.status}`);
  return res.json();
}

export async function getRandomTerm(): Promise<string> {
  const res = await fetch(`${API_BASE}/models/random-term`);
  if (!res.ok) throw new Error(`Random term failed: ${res.status}`);
  const data = await res.json();
  return data.term;
}
