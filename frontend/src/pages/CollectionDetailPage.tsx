import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getCollection, removeFromCollection, updateCollection } from '../api';
import type { CollectionDetailDto, ModelDto } from '../api';
import { useAuth } from '../contexts/AuthContext';
import ModelDetail from '../components/ModelDetail';
import Logo from '../components/Logo';
import { SOURCE_COLORS } from '../sourceConfig';
import './CollectionDetailPage.css';

export default function CollectionDetailPage() {
    const { id } = useParams<{ id: string }>();
    const { user, logout } = useAuth();
    const [collection, setCollection] = useState<CollectionDetailDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selectedModel, setSelectedModel] = useState<ModelDto | null>(null);
    const [editing, setEditing] = useState(false);
    const [editName, setEditName] = useState('');
    const [editDesc, setEditDesc] = useState('');

    const loadCollection = async () => {
        if (!id) return;
        try {
            const data = await getCollection(id);
            setCollection(data);
            setEditName(data.name);
            setEditDesc(data.description || '');
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to load');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadCollection();
    }, [id]);

    const handleRemove = async (itemId: string) => {
        if (!id || !confirm('Remove this model from the collection?')) return;
        try {
            await removeFromCollection(id, itemId);
            loadCollection();
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to remove');
        }
    };

    const handleSaveEdit = async () => {
        if (!id || !editName.trim()) return;
        try {
            await updateCollection(id, { name: editName.trim(), description: editDesc.trim() });
            setEditing(false);
            loadCollection();
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to update');
        }
    };

    return (
        <div className="coldetail-page">
            <header className="collections-header">
                <Link to="/search" className="collections-header__brand">
                    <Logo size={32} />
                    <h1 className="collections-header__title">ModelVault</h1>
                </Link>
                <div className="collections-header__actions">
                    <Link to="/search" className="header-link">🔍 Search</Link>
                    <Link to="/collections" className="header-link">📁 Collections</Link>
                    {user && (
                        <div className="header-user">
                            {user.avatarUrl && <img src={user.avatarUrl} alt="" className="header-avatar" />}
                            <button className="header-logout" onClick={logout}>Sign Out</button>
                        </div>
                    )}
                </div>
            </header>

            <main className="coldetail-main">
                {loading ? (
                    <div className="coldetail-loading">
                        <span className="spinner"></span>
                        <span>Loading collection...</span>
                    </div>
                ) : error ? (
                    <div className="collections-error">⚠️ {error}</div>
                ) : collection ? (
                    <>
                        <div className="coldetail-top">
                            {editing ? (
                                <div className="coldetail-edit-form">
                                    <input
                                        type="text"
                                        value={editName}
                                        onChange={(e) => setEditName(e.target.value)}
                                        className="create-input"
                                    />
                                    <input
                                        type="text"
                                        value={editDesc}
                                        onChange={(e) => setEditDesc(e.target.value)}
                                        placeholder="Description"
                                        className="create-input"
                                    />
                                    <div className="coldetail-edit-actions">
                                        <button className="btn-save" onClick={handleSaveEdit}>Save</button>
                                        <button className="btn-cancel" onClick={() => setEditing(false)}>Cancel</button>
                                    </div>
                                </div>
                            ) : (
                                <>
                                    <div>
                                        <h2 className="coldetail-name">{collection.name}</h2>
                                        {collection.description && (
                                            <p className="coldetail-desc">{collection.description}</p>
                                        )}
                                        <p className="coldetail-meta">
                                            {collection.items.length} {collection.items.length === 1 ? 'model' : 'models'}
                                            {' · '}by {collection.ownerName}
                                        </p>
                                    </div>
                                    {collection.isOwner && (
                                        <button className="btn-create" onClick={() => setEditing(true)}>
                                            ✏️ Edit
                                        </button>
                                    )}
                                </>
                            )}
                        </div>

                        {collection.items.length === 0 ? (
                            <div className="collections-empty">
                                <span className="collections-empty__icon">📭</span>
                                <h3>No models yet</h3>
                                <p>Search for models and save them to this collection!</p>
                                <Link to="/search" className="btn-create" style={{ display: 'inline-block', marginTop: '1rem', textDecoration: 'none' }}>
                                    Go to Search
                                </Link>
                            </div>
                        ) : (
                            <div className="coldetail-grid">
                                {collection.items.map(item => (
                                    <div key={item.id} className="coldetail-card">
                                        <div
                                            className="coldetail-card__img"
                                            onClick={() => {
                                                // Create a minimal ModelDto for the detail view
                                                setSelectedModel({
                                                    id: 0,
                                                    externalId: item.externalId,
                                                    source: item.source,
                                                    sourceUrl: item.sourceUrl,
                                                    title: item.title,
                                                    thumbnailUrl: item.thumbnailUrl,
                                                    description: '',
                                                    tags: [],
                                                    imageUrls: [item.thumbnailUrl],
                                                    creatorName: '',
                                                    creatorProfileUrl: '',
                                                    price: 0,
                                                    currency: '',
                                                    isFree: true,
                                                    isSubscriptionGated: false,
                                                    likeCount: 0,
                                                    license: null,
                                                    category: null,
                                                    createdAtSource: item.addedAt,
                                                } as ModelDto);
                                            }}
                                        >
                                            <img src={item.thumbnailUrl} alt={item.title} />
                                        </div>
                                        <div className="coldetail-card__body">
                                            <div
                                                className="coldetail-card__source"
                                                style={{ background: SOURCE_COLORS[item.source] || '#666' }}
                                            >
                                                {item.source}
                                            </div>
                                            <h4 className="coldetail-card__title">{item.title}</h4>
                                            <div className="coldetail-card__actions">
                                                <a
                                                    href={item.sourceUrl}
                                                    target="_blank"
                                                    rel="noopener noreferrer"
                                                    className="coldetail-card__link"
                                                >
                                                    View on {item.source} ↗
                                                </a>
                                                {collection.isOwner && (
                                                    <button
                                                        className="coldetail-card__remove"
                                                        onClick={() => handleRemove(item.id)}
                                                    >
                                                        🗑️
                                                    </button>
                                                )}
                                            </div>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}
                    </>
                ) : null}
            </main>

            {selectedModel && (
                <ModelDetail
                    model={selectedModel}
                    onClose={() => setSelectedModel(null)}
                />
            )}
        </div>
    );
}
