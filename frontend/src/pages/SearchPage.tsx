import { useState, useEffect, useCallback, useMemo } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { searchModels, getTrending, getRandomTerm } from '../api';
import type { SearchParams, SearchResponse, ModelDto } from '../api';
import { SOURCE_COLORS, SOURCE_ICONS } from '../sourceConfig';
import ModelCard from '../components/ModelCard';
import ModelDetail from '../components/ModelDetail';
import Logo from '../components/Logo';
import './SearchPage.css';

const SORT_OPTIONS = [
    { value: 'relevance', label: 'Relevance' },
    { value: 'newest', label: 'Newest' },

    { value: 'likes', label: 'Most Liked' },
    { value: 'price_asc', label: 'Price: Low → High' },
    { value: 'price_desc', label: 'Price: High → Low' },
];

const SOURCES = ['Thingiverse', 'Cults3D', 'MyMiniFactory', 'Printables', 'MakerWorld'];

export default function SearchPage() {
    const [searchParams, setSearchParams] = useSearchParams();
    
    // Derived state from URL
    const query = searchParams.get('q') || '';
    const page = parseInt(searchParams.get('page') || '1', 10);
    const sortBy = searchParams.get('sort') || 'relevance';
    const freeOnly = searchParams.get('free') === 'true';
    const sourceParams = searchParams.getAll('source');
    const selectedSources = useMemo(() => sourceParams, [sourceParams.join(',')]);

    // UI State
    const [searchInput, setSearchInput] = useState(query);
    const [results, setResults] = useState<SearchResponse | null>(null);
    const [trendingResults, setTrendingResults] = useState<SearchResponse | null>(null);
    const [loading, setLoading] = useState(false);
    const [trendingLoading, setTrendingLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selectedModel, setSelectedModel] = useState<ModelDto | null>(null);
    const [showFilters, setShowFilters] = useState(true);
    const [isMobileFilterOpen, setIsMobileFilterOpen] = useState(false);

    const isSearchActive = query.trim().length > 0;

    // Sync searchInput with URL query when URL changes (e.g. back button)
    useEffect(() => {
        setSearchInput(query);
    }, [query]);

    // Load trending models on mount
    useEffect(() => {
        const loadTrending = async () => {
            setTrendingLoading(true);
            try {
                const data = await getTrending(1, 24);
                setTrendingResults(data);
            } catch (err) {
                console.error('Failed to load trending:', err);
            } finally {
                setTrendingLoading(false);
            }
        };
        loadTrending();
    }, []); // Only run once on mount

    const doSearch = useCallback(async () => {
        if (!query.trim()) {
            setResults(null);
            return;
        }

        setLoading(true);
        setError(null);

        const params: SearchParams = {
            q: query,
            page,
            pageSize: 24,
            sortBy,
            freeOnly: freeOnly || undefined,
            sources: selectedSources.length > 0 ? selectedSources : undefined
        };

        try {
            const response = await searchModels(params);
            setResults(response);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Search failed');
            setResults(null);
        } finally {
            setLoading(false);
        }
    }, [query, page, sortBy, freeOnly, selectedSources]);

    // Trigger search when URL params change
    useEffect(() => {
        if (isSearchActive) {
            doSearch();
        } else {
            setResults(null);
        }
    }, [doSearch, isSearchActive]);

    const updateParams = (newParams: Record<string, string | string[] | null>) => {
        setSearchParams(prev => {
            const next = new URLSearchParams(prev);
            Object.entries(newParams).forEach(([key, value]) => {
                if (value === null) {
                    next.delete(key);
                } else if (Array.isArray(value)) {
                    next.delete(key);
                    value.forEach(v => next.append(key, v));
                } else {
                    next.set(key, value);
                }
            });
            return next;
        });
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    const handleSearch = (e: React.FormEvent) => {
        e.preventDefault();
        // Set query, reset page to 1
        updateParams({ q: searchInput, page: '1' });
    };

    const handleSort = (newSort: string) => {
        updateParams({ sort: newSort, page: '1' });
    };

    const toggleSource = (source: string) => {
        const newSources = selectedSources.includes(source)
            ? selectedSources.filter(s => s !== source)
            : [...selectedSources, source];
        
        updateParams({ source: newSources.length > 0 ? newSources : null, page: '1' });
    };

    const handlePage = (newPage: number) => {
        updateParams({ page: newPage.toString() });
    };

    const handleClearSearch = () => {
        setSearchInput('');
        setSearchParams({}); // Clear all params to go back to trending
    };

    const handleRandom = async () => {
        try {
            const term = await getRandomTerm();
            setSearchInput(term);
            updateParams({ q: term, page: '1' });
        } catch (error) {
            console.error(error);
        }
    };

    const displayResults = isSearchActive ? results : null;

    return (
        <div className="search-page">
            {/* Header */}
            <header className="search-header">
                <Link to="/" className="search-header__brand">
                    <Logo size={32} />
                    <h1 className="search-header__title">ModelVault</h1>
                    <span className="search-header__tagline">3D Model Search Aggregator</span>
                </Link>

                <form className="search-bar" onSubmit={handleSearch}>
                    <div className="search-bar__input-wrapper">
                        <span className="search-bar__icon">🔍</span>
                        <input
                            type="text"
                            className="search-bar__input"
                            placeholder="Search 3D models across all platforms..."
                            value={searchInput}
                            onChange={(e) => setSearchInput(e.target.value)}
                            id="main-search"
                        />
                        {searchInput && (
                            <button
                                type="button"
                                className="search-bar__clear"
                                onClick={handleClearSearch}
                            >
                                ✕
                            </button>
                        )}
                    </div>
                    <button type="submit" className="search-bar__submit">
                        Search
                    </button>
                    <div className="search-bar__actions">
                        <button type="button" className="search-bar__random" onClick={handleRandom} title="Surprise Me!">
                            🎲
                        </button>
                        <button type="button" className="search-bar__filter-mobile" onClick={() => setIsMobileFilterOpen(true)} title="Filters">
                            ⚙️ Filters
                        </button>
                    </div>
                </form>
            </header>

            <div className="search-layout">
                {/* Mobile Filter Overlay */}
                <div 
                    className={`search-sidebar-overlay ${isMobileFilterOpen ? 'open' : ''}`} 
                    onClick={() => setIsMobileFilterOpen(false)}
                ></div>

                {/* Sidebar */}
                <aside className={`search-sidebar ${showFilters ? '' : 'collapsed'} ${isMobileFilterOpen ? 'mobile-open' : ''}`}>
                    <div className="search-sidebar__mobile-header">
                        <h2>Filters</h2>
                        <button className="search-sidebar__close" onClick={() => setIsMobileFilterOpen(false)}>✕</button>
                    </div>

                    <button
                        className="sidebar-toggle desktop-only"
                        onClick={() => setShowFilters(!showFilters)}
                    >
                        {showFilters ? '◀ Filters' : '▶'}
                    </button>

                    {showFilters && (
                        <>
                            <div className="filter-section">
                                <h3 className="filter-title">Sources</h3>
                                {SOURCES.map(source => (
                                    <label key={source} className="filter-checkbox">
                                        <input
                                            type="checkbox"
                                            checked={selectedSources.includes(source)}
                                            onChange={() => toggleSource(source)}
                                        />
                                        <span 
                                            className="filter-checkbox__mark" 
                                            style={{ borderColor: selectedSources.includes(source) ? SOURCE_COLORS[source] : undefined }}
                                        ></span>
                                        <span className="source-indicator">
                                            {SOURCE_ICONS[source]} {source}
                                        </span>
                                    </label>
                                ))}
                            </div>

                            <div className="filter-section">
                                <h3 className="filter-title">Pricing</h3>
                                <label className="filter-checkbox">
                                    <input
                                        type="checkbox"
                                        checked={freeOnly}
                                        onChange={(e) => {
                                            updateParams({ free: e.target.checked ? 'true' : null, page: '1' });
                                        }}
                                    />
                                    <span className="filter-checkbox__mark"></span>
                                    <span>Free only</span>
                                </label>
                            </div>

                            {isSearchActive && (
                                <div className="filter-section">
                                    <h3 className="filter-title">Sort By</h3>
                                    <div className="sort-options">
                                        {SORT_OPTIONS.map(opt => (
                                            <button
                                                key={opt.value}
                                                className={`sort-btn ${sortBy === opt.value ? 'active' : ''}`}
                                                onClick={() => handleSort(opt.value)}
                                            >
                                                {opt.label}
                                            </button>
                                        ))}
                                    </div>
                                </div>
                            )}
                        </>
                    )}
                </aside>

                {/* Main Content */}
                <main className="search-main">
                    {/* Trending Section (shown when no search query) */}
                    {!isSearchActive && (
                        <div className="trending-section">
                            <div className="trending-header">
                                <h2 className="trending-title">
                                    <span className="trending-icon">🔥</span>
                                    Trending Models
                                </h2>
                                <p className="trending-subtitle">Popular models from all platforms</p>
                            </div>

                            {trendingLoading ? (
                                <div className="trending-loading">
                                    <span className="spinner"></span>
                                    <span>Loading trending models...</span>
                                </div>
                            ) : trendingResults && trendingResults.results.length > 0 ? (
                                <div className="results-grid">
                                    {trendingResults.results.map((model, index) => (
                                        <ModelCard
                                            key={`trending-${model.source}-${model.externalId}-${index}`}
                                            model={model}
                                            onClick={() => setSelectedModel(model)}
                                        />
                                    ))}
                                </div>
                            ) : (
                                <div className="trending-empty">
                                    <p>Could not load trending models. Make sure the backend is running.</p>
                                </div>
                            )}
                        </div>
                    )}

                    {/* Search Results (shown when search is active) */}
                    {isSearchActive && (
                        <>
                            {/* Status Bar */}
                            <div className="search-status">
                                {loading ? (
                                    <span className="search-status__loading">
                                        <span className="spinner"></span> Searching across all platforms...
                                    </span>
                                ) : displayResults ? (
                                    <span>
                                        <strong>{displayResults.totalCount.toLocaleString()}</strong> models found
                                        {query && <> for "<em>{query}</em>"</>}
                                    </span>
                                ) : null}

                                {error && (
                                    <div className="search-error">
                                        ⚠️ {error}
                                        <br />
                                        <small>Make sure the backend API is running on port 5000</small>
                                    </div>
                                )}
                            </div>

                            {/* Results Grid */}
                            <div className="results-grid">
                                {displayResults?.results.map((model, index) => (
                                    <ModelCard
                                        key={`${model.source}-${model.externalId}-${index}`}
                                        model={model}
                                        onClick={() => setSelectedModel(model)}
                                    />
                                ))}
                            </div>

                            {/* Empty State */}
                            {!loading && displayResults?.results.length === 0 && (
                                <div className="search-empty">
                                    <span className="search-empty__icon">🔍</span>
                                    <h3>No models found</h3>
                                    <p>Try a different search term or adjust your filters</p>
                                </div>
                            )}

                            {/* Pagination */}
                            {displayResults && displayResults.totalPages > 1 && (
                                <div className="search-pagination">
                                    <button
                                        className="page-btn"
                                        disabled={page <= 1}
                                        onClick={() => handlePage(page - 1)}
                                    >
                                        ← Previous
                                    </button>

                                    <div className="page-numbers">
                                        {Array.from({ length: Math.min(displayResults.totalPages, 7) }, (_, i) => {
                                            let pageNum: number;
                                            if (displayResults.totalPages <= 7) {
                                                pageNum = i + 1;
                                            } else if (displayResults.page <= 4) {
                                                pageNum = i + 1;
                                            } else if (displayResults.page >= displayResults.totalPages - 3) {
                                                pageNum = displayResults.totalPages - 6 + i;
                                            } else {
                                                pageNum = displayResults.page - 3 + i;
                                            }
                                            return (
                                                <button
                                                    key={pageNum}
                                                    className={`page-num ${page === pageNum ? 'active' : ''}`}
                                                    onClick={() => handlePage(pageNum)}
                                                >
                                                    {pageNum}
                                                </button>
                                            );
                                        })}
                                    </div>

                                    <button
                                        className="page-btn"
                                        disabled={page >= displayResults.totalPages}
                                        onClick={() => handlePage(page + 1)}
                                    >
                                        Next →
                                    </button>
                                </div>
                            )}
                        </>
                    )}
                </main>
            </div>

            {/* Model Detail Slide-out */}
            {selectedModel && (
                <ModelDetail
                    model={selectedModel}
                    onClose={() => setSelectedModel(null)}
                />
            )}
        </div>
    );
}
