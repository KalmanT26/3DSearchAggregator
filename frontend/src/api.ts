const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api';

const TOKEN_KEY = 'modelvault_jwt';

// --- Helper: fetch with auth header ---
function authHeaders(): HeadersInit {
    const token = localStorage.getItem(TOKEN_KEY);
    return token ? { Authorization: `Bearer ${token}` } : {};
}

async function authFetch(url: string, options: RequestInit = {}): Promise<Response> {
    return fetch(url, {
        ...options,
        headers: {
            'Content-Type': 'application/json',
            ...authHeaders(),
            ...options.headers,
        },
    });
}

// =====================
// Models / Search
// =====================

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

export async function getModelDetails(source: string, id: string): Promise<ModelDto> {
    const safeId = encodeURIComponent(id);
    const res = await fetch(`${API_BASE}/models/${source}/${safeId}`);
    if (!res.ok) throw new Error(`Failed to fetch details: ${res.status}`);
    return res.json();
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

// =====================
// Auth
// =====================

export interface UserDto {
    id: string;
    email: string;
    displayName: string;
    avatarUrl: string | null;
}

export interface AuthResponse {
    token: string;
    user: UserDto;
}

export async function postGoogleLogin(idToken: string): Promise<AuthResponse> {
    const res = await fetch(`${API_BASE}/auth/google`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ idToken }),
    });
    if (!res.ok) throw new Error(`Login failed: ${res.status}`);
    return res.json();
}

export async function postManualRegister(email: string, password: string, displayName: string): Promise<AuthResponse> {
    const res = await fetch(`${API_BASE}/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password, displayName }),
    });
    if (!res.ok) {
        const errorText = await res.text();
        throw new Error(errorText || 'Registration failed');
    }
    return res.json();
}

export async function postManualLogin(email: string, password: string): Promise<AuthResponse> {
    const res = await fetch(`${API_BASE}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
    });
    if (!res.ok) {
        const errorText = await res.text();
        throw new Error(errorText || 'Login failed');
    }
    return res.json();
}

export async function getMe(token?: string): Promise<UserDto> {
    const headers: HeadersInit = {
        'Content-Type': 'application/json',
    };
    if (token) {
        headers.Authorization = `Bearer ${token}`;
    } else {
        Object.assign(headers, authHeaders());
    }
    const res = await fetch(`${API_BASE}/auth/me`, { headers });
    if (!res.ok) throw new Error(`Get profile failed: ${res.status}`);
    return res.json();
}

// =====================
// Collections
// =====================

export interface CollectionSummaryDto {
    id: string;
    name: string;
    description: string | null;
    isPublic: boolean;
    itemCount: number;
    createdAt: string;
    updatedAt: string;
}

export interface CollectionItemDto {
    id: string;
    source: string;
    externalId: string;
    title: string;
    thumbnailUrl: string;
    sourceUrl: string;
    addedAt: string;
}

export interface CollectionDetailDto {
    id: string;
    name: string;
    description: string | null;
    isPublic: boolean;
    isOwner: boolean;
    ownerName: string;
    createdAt: string;
    updatedAt: string;
    items: CollectionItemDto[];
}

export async function getCollections(): Promise<CollectionSummaryDto[]> {
    const res = await authFetch(`${API_BASE}/collections`);
    if (!res.ok) throw new Error(`Failed to fetch collections: ${res.status}`);
    return res.json();
}

export async function createCollection(name: string, description?: string, isPublic = false): Promise<CollectionSummaryDto> {
    const res = await authFetch(`${API_BASE}/collections`, {
        method: 'POST',
        body: JSON.stringify({ name, description, isPublic }),
    });
    if (!res.ok) throw new Error(`Failed to create collection: ${res.status}`);
    return res.json();
}

export async function getCollection(id: string): Promise<CollectionDetailDto> {
    const res = await authFetch(`${API_BASE}/collections/${id}`);
    if (!res.ok) throw new Error(`Failed to fetch collection: ${res.status}`);
    return res.json();
}

export async function updateCollection(id: string, data: { name?: string; description?: string; isPublic?: boolean }): Promise<void> {
    const res = await authFetch(`${API_BASE}/collections/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
    });
    if (!res.ok) throw new Error(`Failed to update collection: ${res.status}`);
}

export async function deleteCollection(id: string): Promise<void> {
    const res = await authFetch(`${API_BASE}/collections/${id}`, {
        method: 'DELETE',
    });
    if (!res.ok) throw new Error(`Failed to delete collection: ${res.status}`);
}

export async function addToCollection(collectionId: string, item: {
    source: string;
    externalId: string;
    title: string;
    thumbnailUrl: string;
    sourceUrl: string;
}): Promise<CollectionItemDto> {
    const res = await authFetch(`${API_BASE}/collections/${collectionId}/items`, {
        method: 'POST',
        body: JSON.stringify(item),
    });
    if (!res.ok) {
        if (res.status === 409) throw new Error('Already in collection');
        throw new Error(`Failed to add to collection: ${res.status}`);
    }
    return res.json();
}

export async function removeFromCollection(collectionId: string, itemId: string): Promise<void> {
    const res = await authFetch(`${API_BASE}/collections/${collectionId}/items/${itemId}`, {
        method: 'DELETE',
    });
    if (!res.ok) throw new Error(`Failed to remove from collection: ${res.status}`);
}
