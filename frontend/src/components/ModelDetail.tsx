import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { type ModelDto, getModelDetails } from '../api';
import { SOURCE_COLORS, SOURCE_ICONS } from '../sourceConfig';
import './ModelDetail.css';

interface Props {
    model: ModelDto;
    onClose: () => void;
}

export default function ModelDetail({ model: initialModel, onClose }: Props) {
    const [model, setModel] = useState<ModelDto>(initialModel);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [selectedImage, setSelectedImage] = useState<string | null>(initialModel.thumbnailUrl || (initialModel.imageUrls.length > 0 ? initialModel.imageUrls[0] : null));

    useEffect(() => {
        let active = true;
        setLoading(true);
        setError('');

        getModelDetails(initialModel.source, initialModel.externalId)
            .then(details => {
                if (active) {
                    setModel(details);
                    // Update selected image if not already set or if we get better/more images
                    if (!selectedImage && details.thumbnailUrl) {
                        setSelectedImage(details.thumbnailUrl);
                    } else if (details.imageUrls.length > 0 && !selectedImage) {
                         setSelectedImage(details.imageUrls[0]);
                    }
                    setLoading(false);
                }
            })
            .catch(err => {
                if (active) {
                    console.error("Failed to load details", err);
                    setError('Failed to load full details. Showing summary.');
                    setLoading(false);
                }
            });

        return () => { active = false; };
    }, [initialModel]);

    // Update selected image when model prop changes (e.g. for initial load)
    useEffect(() => {
         setSelectedImage(model.thumbnailUrl || (model.imageUrls.length > 0 ? model.imageUrls[0] : null));
    }, [model.thumbnailUrl, model.imageUrls]);


    return (
        <div className="detail-overlay" onClick={onClose}>
            <div className="detail-panel" onClick={(e) => e.stopPropagation()}>
                <button className="detail-close" onClick={onClose}>✕</button>

                <div className="detail-content-scroll">
                    <div className="detail-images">
                        {selectedImage ? (
                            <img src={selectedImage} alt={model.title} className="detail-main-image" />
                        ) : (
                            <div className="detail-no-image">📦 No Preview Available</div>
                        )}
                        {model.imageUrls.length > 1 && (
                            <div className="detail-image-strip">
                                {model.imageUrls.slice(0, 5).map((url, i) => (
                                    <img 
                                        key={i} 
                                        src={url} 
                                        alt={`${model.title} ${i + 1}`} 
                                        className={`detail-thumb ${selectedImage === url ? 'active' : ''}`}
                                        onClick={() => setSelectedImage(url)}
                                    />
                                ))}
                            </div>
                        )}
                    </div>

                    <div className="detail-info">
                        <div className="detail-header">
                            <h2 className="detail-title">{model.title}</h2>
                            <div className={`detail-price ${model.isFree ? 'free' : 'paid'}`}>
                                {model.isFree ? 'FREE' : `${model.currency === 'EUR' ? '€' : '$'}${model.price.toFixed(2)}`}
                            </div>
                        </div>

                        <div className="detail-creator">
                            by <a href={model.creatorProfileUrl} target="_blank" rel="noopener noreferrer">
                                {model.creatorName || 'Unknown'}
                            </a>
                        </div>

                        <div className="detail-meta">
                            <div className="detail-meta-item">
                                <span className="detail-meta-icon">🌐</span>
                                <span className="detail-meta-label">Source</span>
                                <span className="detail-meta-value source-badge" style={{ backgroundColor: SOURCE_COLORS[model.source] || '#333' }}>
                                    {SOURCE_ICONS[model.source] || '🔹'} {model.source}
                                </span>
                            </div>

                            <div className="detail-meta-item">
                                <span className="detail-meta-icon">❤️</span>
                                <span className="detail-meta-label">Likes</span>
                                <span className="detail-meta-value">{model.likeCount.toLocaleString()}</span>
                            </div>
                            
                            {(model.viewCount || 0) > 0 && (
                                <div className="detail-meta-item">
                                    <span className="detail-meta-icon">👁️</span>
                                    <span className="detail-meta-label">Views</span>
                                    <span className="detail-meta-value">{model.viewCount?.toLocaleString()}</span>
                                </div>
                            )}

                             {(model.makeCount || 0) > 0 && (
                                <div className="detail-meta-item">
                                    <span className="detail-meta-icon">🔨</span>
                                    <span className="detail-meta-label">Makes</span>
                                    <span className="detail-meta-value">{model.makeCount?.toLocaleString()}</span>
                                </div>
                            )}

                            {model.license && (
                                <div className="detail-meta-item">
                                    <span className="detail-meta-icon">📄</span>
                                    <span className="detail-meta-label">License</span>
                                    <span className="detail-meta-value">{model.license}</span>
                                </div>
                            )}
                        </div>

                        {loading && <div className="detail-loading">Loading full details...</div>}
                        {error && <div className="detail-error">{error}</div>}

                        {model.isSubscriptionGated && (
                            <div className="detail-warning">
                                🔒 This model may require a subscription or membership to download
                            </div>
                        )}

                        {model.tags.length > 0 && (
                            <div className="detail-tags">
                                {model.tags.slice(0, 15).map((tag, i) => (
                                    <Link 
                                        key={i} 
                                        to={`/search?q=${encodeURIComponent(tag)}`}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        className="detail-tag"
                                    >
                                        {tag}
                                    </Link>
                                ))}
                            </div>
                        )}

                        <div className="detail-description">
                            <h4>Description</h4>
                            {model.descriptionHtml ? (
                                <div 
                                    className="detail-html"
                                    dangerouslySetInnerHTML={{ __html: model.descriptionHtml }} 
                                />
                            ) : (
                                <p>{model.description || 'No description available.'}</p>
                            )}
                        </div>

                        <div className="detail-actions">
                            <a
                                href={model.sourceUrl}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="detail-btn primary"
                            >
                                View on {model.source} {SOURCE_ICONS[model.source] || '→'}
                            </a>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
