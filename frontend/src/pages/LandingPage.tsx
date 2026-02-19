import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getRandomTerm } from '../api';
import Logo from '../components/Logo';
import { SOURCE_ICONS, SOURCE_COLORS } from '../sourceConfig';
import './LandingPage.css';

const SUPPORTED_SOURCES = ['Thingiverse', 'Cults3D', 'MyMiniFactory', 'Printables', 'MakerWorld'];

export default function LandingPage() {
    const [searchInput, setSearchInput] = useState('');
    const navigate = useNavigate();

    const handleSearch = (e: React.FormEvent) => {
        e.preventDefault();
        if (searchInput.trim()) {
            navigate(`/search?q=${encodeURIComponent(searchInput)}`);
        }
    };

    const handleRandom = async () => {
        try {
            const term = await getRandomTerm();
            setSearchInput(term);
            navigate(`/search?q=${encodeURIComponent(term)}`);
        } catch (error) {
            console.error(error);
        }
    };

    return (
        <div className="landing-page">
            <div className="landing-hero">
                <div className="landing-hero__content">
                    <div className="landing-brand">
                        <Logo size={80} className="landing-brand__logo" />
                        <h1 className="landing-brand__title">ModelVault</h1>
                    </div>
                    <p className="landing-hero__subtitle">
                        Discover 3D models from Thingiverse, Cults3D, MyMiniFactory, and Printables in one place.
                    </p>
                    
                    <form className="landing-search" onSubmit={handleSearch}>
                        <input
                            type="text"
                            className="landing-search__input"
                            placeholder="What do you want to print today?"
                            value={searchInput}
                            onChange={(e) => setSearchInput(e.target.value)}
                            autoFocus
                        />
                        <button type="submit" className="landing-search__button">
                            Search
                        </button>
                        <button type="button" className="landing-search__random" onClick={handleRandom} title="Surprise Me!">
                            🎲
                        </button>
                    </form>

                    <div className="landing-tags">
                        <span>Popular:</span>
                        <button onClick={() => navigate('/search?q=dragon')}>Dragon</button>
                        <button onClick={() => navigate('/search?q=vase')}>Vase</button>
                        <button onClick={() => navigate('/search?q=phone+stand')}>Phone Stand</button>
                        <button onClick={() => navigate('/search?q=puzzle')}>Puzzle</button>
                    </div>

                    <div className="landing-sources">
                        {SUPPORTED_SOURCES.map((source) => (
                            <div 
                                key={source} 
                                className="landing-source-badge"
                                style={{ borderColor: `${SOURCE_COLORS[source]}33` }} // 20% opacity border
                            >
                                <span className="landing-source-badge__icon">{SOURCE_ICONS[source]}</span>
                                <span style={{ color: typeof SOURCE_COLORS[source] === 'string' ? SOURCE_COLORS[source] : undefined }}>
                                    {source}
                                </span>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
}
