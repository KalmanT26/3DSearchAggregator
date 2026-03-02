import { useState } from 'react';
import type { ModelDto } from '../api';
import { SOURCE_COLORS, SOURCE_ICONS } from '../sourceConfig';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import SaveToCollectionModal from './SaveToCollectionModal';
import './ModelCard.css';

interface Props {
    model: ModelDto;
    onClick: () => void;
}

export default function ModelCard({ model, onClick }: Props) {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [showSave, setShowSave] = useState(false);

    const formatNumber = (n: number) => {
        if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
        if (n >= 1000) return (n / 1000).toFixed(1) + 'K';
        return n.toString();
    };

    return (
        <>
            <div className="model-card" onClick={onClick} tabIndex={0} role="button">
                <div className="model-card__image-wrapper">
                    {model.thumbnailUrl ? (
                        <img
                            src={model.thumbnailUrl}
                            alt={model.title}
                            className="model-card__image"
                            loading="lazy"
                            onError={(e) => {
                                (e.target as HTMLImageElement).src =
                                    'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200"><rect fill="%231a1a2e" width="200" height="200"/><text fill="%236b6b8d" font-size="14" x="50%" y="50%" text-anchor="middle" dy=".3em">No Image</text></svg>';
                            }}
                        />
                    ) : (
                        <div className="model-card__no-image">
                            <span>📦</span>
                            <span>No Preview</span>
                        </div>
                    )}

                    {/* Price Badge */}
                    <div className={`model-card__price ${model.isFree ? 'free' : 'paid'}`}>
                        {model.isFree ? 'FREE' : `${model.currency === 'EUR' ? '€' : '$'}${model.price.toFixed(2)}`}
                    </div>

                    {/* Source Badge */}
                    <div
                        className="model-card__source"
                        style={{ backgroundColor: SOURCE_COLORS[model.source] || '#333' }}
                    >
                        {SOURCE_ICONS[model.source] || '🔹'} {model.source}
                    </div>

                    {/* Save Button */}
                    <button
                        className="model-card__save"
                        onClick={(e) => {
                            e.stopPropagation();
                            if (!user) {
                                navigate('/login');
                                return;
                            }
                            setShowSave(true);
                        }}
                        title="Save to collection"
                    >
                        🔖
                    </button>
                </div>

                <div className="model-card__content">
                    <h3 className="model-card__title" title={model.title}>
                        {model.title}
                    </h3>

                    <div className="model-card__creator">
                        by <span className="model-card__creator-name">{model.creatorName || 'Unknown'}</span>
                    </div>

                    <div className="model-card__stats">

                        <span title="Likes">❤️ {formatNumber(model.likeCount)}</span>
                    </div>

                    {model.isSubscriptionGated && (
                        <div className="model-card__subscription-notice">
                            🔒 Subscription may be required
                        </div>
                    )}
                </div>
            </div>

            {showSave && (
                <SaveToCollectionModal
                    model={model}
                    onClose={() => setShowSave(false)}
                />
            )}
        </>
    );
}
