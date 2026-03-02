import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { getCollections, createCollection, deleteCollection } from '../api';
import type { CollectionSummaryDto } from '../api';
import { useAuth } from '../contexts/AuthContext';
import Logo from '../components/Logo';
import './CollectionsPage.css';

export default function CollectionsPage() {
    const { user, logout } = useAuth();
    const [collections, setCollections] = useState<CollectionSummaryDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [showCreate, setShowCreate] = useState(false);
    const [newName, setNewName] = useState('');
    const [newDesc, setNewDesc] = useState('');
    const [error, setError] = useState<string | null>(null);

    const loadCollections = async () => {
        try {
            const data = await getCollections();
            setCollections(data);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to load');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadCollections();
    }, []);

    const handleCreate = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!newName.trim()) return;
        try {
            await createCollection(newName.trim(), newDesc.trim() || undefined);
            setNewName('');
            setNewDesc('');
            setShowCreate(false);
            loadCollections();
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to create');
        }
    };

    const handleDelete = async (id: string, name: string) => {
        if (!confirm(`Delete "${name}" and all its items?`)) return;
        try {
            await deleteCollection(id);
            loadCollections();
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to delete');
        }
    };

    return (
        <div className="collections-page">
            <header className="collections-header">
                <Link to="/search" className="collections-header__brand">
                    <Logo size={32} />
                    <h1 className="collections-header__title">ModelVault</h1>
                </Link>
                <div className="collections-header__actions">
                    <Link to="/search" className="header-link">🔍 Search</Link>
                    {user && (
                        <div className="header-user">
                            {user.avatarUrl && <img src={user.avatarUrl} alt="" className="header-avatar" />}
                            <span className="header-name">{user.displayName}</span>
                            <button className="header-logout" onClick={logout}>Sign Out</button>
                        </div>
                    )}
                </div>
            </header>

            <main className="collections-main">
                <div className="collections-top">
                    <h2 className="collections-heading">📁 My Collections</h2>
                    <button
                        className="btn-create"
                        onClick={() => setShowCreate(!showCreate)}
                    >
                        {showCreate ? '✕ Cancel' : '+ New Collection'}
                    </button>
                </div>

                {error && <div className="collections-error">⚠️ {error}</div>}

                {showCreate && (
                    <form className="create-form" onSubmit={handleCreate}>
                        <input
                            type="text"
                            placeholder="Collection name..."
                            value={newName}
                            onChange={(e) => setNewName(e.target.value)}
                            className="create-input"
                            autoFocus
                            required
                        />
                        <input
                            type="text"
                            placeholder="Description (optional)"
                            value={newDesc}
                            onChange={(e) => setNewDesc(e.target.value)}
                            className="create-input"
                        />
                        <button type="submit" className="btn-save">Create Collection</button>
                    </form>
                )}

                {loading ? (
                    <div className="collections-loading">
                        <span className="spinner"></span>
                        <span>Loading collections...</span>
                    </div>
                ) : collections.length === 0 ? (
                    <div className="collections-empty">
                        <span className="collections-empty__icon">📦</span>
                        <h3>No collections yet</h3>
                        <p>Create your first collection to start saving 3D models!</p>
                    </div>
                ) : (
                    <div className="collections-grid">
                        {collections.map(col => (
                            <div key={col.id} className="collection-card">
                                <Link to={`/collections/${col.id}`} className="collection-card__link">
                                    <div className="collection-card__icon">📁</div>
                                    <h3 className="collection-card__name">{col.name}</h3>
                                    {col.description && (
                                        <p className="collection-card__desc">{col.description}</p>
                                    )}
                                    <div className="collection-card__meta">
                                        <span>{col.itemCount} {col.itemCount === 1 ? 'model' : 'models'}</span>
                                        <span>{new Date(col.updatedAt).toLocaleDateString()}</span>
                                    </div>
                                </Link>
                                <button
                                    className="collection-card__delete"
                                    onClick={(e) => { e.preventDefault(); handleDelete(col.id, col.name); }}
                                    title="Delete collection"
                                >
                                    🗑️
                                </button>
                            </div>
                        ))}
                    </div>
                )}
            </main>
        </div>
    );
}
