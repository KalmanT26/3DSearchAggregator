import { useState, useEffect, type ReactNode } from 'react';
import './AuthGate.css';

interface Props {
    children: ReactNode;
}

const STORAGE_KEY = 'modelvault_auth_token';

export default function AuthGate({ children }: Props) {
    // 1. If we are in DEV mode, always allow access
    const isDev = import.meta.env.DEV;
    
    // 2. Initialize state
    const [isAuthenticated, setIsAuthenticated] = useState(isDev);
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');

    useEffect(() => {
        if (isDev) return;

        // Check if previously authenticated
        const token = localStorage.getItem(STORAGE_KEY);
        if (token === 'authenticated') {
            setIsAuthenticated(true);
        }
    }, [isDev]);

    const handleLogin = (e: React.FormEvent) => {
        e.preventDefault();
        const sitePassword = import.meta.env.VITE_SITE_PASSWORD;

        if (password === sitePassword) {
            localStorage.setItem(STORAGE_KEY, 'authenticated');
            setIsAuthenticated(true);
            setError('');
        } else {
            setError('Incorrect password');
        }
    };

    if (isAuthenticated) {
        return <>{children}</>;
    }

    return (
        <div className="auth-container">
            <div className="auth-card">
                <div className="auth-icon">🔒</div>
                <h2 className="auth-title">Restricted Access</h2>
                <p className="auth-subtitle">Please enter the access password to continue.</p>
                
                <form onSubmit={handleLogin} className="auth-form">
                    <input
                        type="password"
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        placeholder="Enter password..."
                        className="auth-input"
                        autoFocus
                    />
                    {error && <div className="auth-error">{error}</div>}
                    <button type="submit" className="auth-button">
                        Unlock Access
                    </button>
                </form>
            </div>
        </div>
    );
}
