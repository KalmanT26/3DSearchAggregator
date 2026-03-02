import { useState, useEffect } from 'react';
import { getCollections, createCollection, addToCollection } from '../api';
import type { CollectionSummaryDto, ModelDto } from '../api';
import './SaveToCollectionModal.css';

interface Props {
    model: ModelDto;
    onClose: () => void;
}

export default function SaveToCollectionModal({ model, onClose }: Props) {
    const [collections, setCollections] = useState<CollectionSummaryDto[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState<string | null>(null);
    const [saved, setSaved] = useState<Set<string>>(new Set());
    const [error, setError] = useState<string | null>(null);
    const [showCreate, setShowCreate] = useState(false);
    const [newName, setNewName] = useState('');

    useEffect(() => {
        getCollections()
            .then(setCollections)
            .catch(() => setError('Failed to load collections'))
            .finally(() => setLoading(false));
    }, []);

    const handleSave = async (collectionId: string) => {
        setSaving(collectionId);
        setError(null);
        try {
            await addToCollection(collectionId, {
                source: model.source,
                externalId: model.externalId,
                title: model.title,
                thumbnailUrl: model.thumbnailUrl,
                sourceUrl: model.sourceUrl,
            });
            setSaved(prev => new Set(prev).add(collectionId));
        } catch (err) {
            if (err instanceof Error && err.message.includes('Already')) {
                setSaved(prev => new Set(prev).add(collectionId));
            } else {
                setError(err instanceof Error ? err.message : 'Failed to save');
            }
        } finally {
            setSaving(null);
        }
    };

    const handleCreate = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!newName.trim()) return;
        try {
            const col = await createCollection(newName.trim());
            setCollections(prev => [col, ...prev]);
            setNewName('');
            setShowCreate(false);
            // Auto-save to the new collection
            handleSave(col.id);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to create');
        }
    };

    return (
        <div className="save-modal-overlay" onClick={onClose}>
            <div className="save-modal" onClick={(e) => e.stopPropagation()}>
                <div className="save-modal__header">
                    <h3>Save to Collection</h3>
                    <button className="save-modal__close" onClick={onClose}>✕</button>
                </div>

                <div className="save-modal__model">
                    <img src={model.thumbnailUrl} alt="" className="save-modal__thumb" />
                    <span className="save-modal__title">{model.title}</span>
                </div>

                {error && <div className="save-modal__error">⚠️ {error}</div>}

                <div className="save-modal__list">
                    {loading ? (
                        <div className="save-modal__loading">Loading...</div>
                    ) : collections.length === 0 && !showCreate ? (
                        <div className="save-modal__empty">
                            No collections yet. Create one below!
                        </div>
                    ) : (
                        collections.map(col => (
                            <button
                                key={col.id}
                                className={`save-modal__item ${saved.has(col.id) ? 'saved' : ''}`}
                                onClick={() => !saved.has(col.id) && handleSave(col.id)}
                                disabled={saving === col.id || saved.has(col.id)}
                            >
                                <span className="save-modal__item-icon">
                                    {saved.has(col.id) ? '✅' : '📁'}
                                </span>
                                <span className="save-modal__item-name">{col.name}</span>
                                <span className="save-modal__item-count">{col.itemCount} items</span>
                                {saving === col.id && <span className="spinner-sm"></span>}
                            </button>
                        ))
                    )}
                </div>

                {showCreate ? (
                    <form className="save-modal__create-form" onSubmit={handleCreate}>
                        <input
                            type="text"
                            placeholder="Collection name..."
                            value={newName}
                            onChange={(e) => setNewName(e.target.value)}
                            className="save-modal__create-input"
                            autoFocus
                        />
                        <button type="submit" className="save-modal__create-btn">Create & Save</button>
                    </form>
                ) : (
                    <button
                        className="save-modal__new-btn"
                        onClick={() => setShowCreate(true)}
                    >
                        + New Collection
                    </button>
                )}
            </div>
        </div>
    );
}
