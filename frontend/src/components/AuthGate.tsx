import { useState, type ReactNode } from 'react';
import { GoogleLogin } from '@react-oauth/google';
import { useAuth } from '../contexts/AuthContext';
import Logo from './Logo';
import './AuthGate.css';

interface Props {
    children: ReactNode;
}

export default function AuthGate({ children }: Props) {
    const { user, isLoading, loginGoogle, loginManual, registerManual } = useAuth();
    
    const [mode, setMode] = useState<'login' | 'register'>('login');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [displayName, setDisplayName] = useState('');
    const [error, setError] = useState<string | null>(null);
    const [isSubmitting, setIsSubmitting] = useState(false);

    if (isLoading) {
        return (
            <div className="auth-container">
                <div className="auth-card">
                    <span className="auth-spinner"></span>
                    <p className="auth-subtitle">Loading...</p>
                </div>
            </div>
        );
    }

    if (user) {
        return <>{children}</>;
    }

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        setIsSubmitting(true);

        try {
            if (mode === 'login') {
                await loginManual(email, password);
            } else {
                await registerManual(email, password, displayName || email.split('@')[0]);
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : 'An error occurred. Please try again.');
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="auth-container">
            <div className="auth-card">
                <Logo size={48} />
                <h2 className="auth-title">Welcome to ModelVault</h2>
                <p className="auth-subtitle">Sign in to search, save, and organize 3D models.</p>

                <div className="auth-tabs">
                    <button 
                        className={`auth-tab ${mode === 'login' ? 'active' : ''}`}
                        onClick={() => { setMode('login'); setError(null); }}
                    >Sign In</button>
                    <button 
                        className={`auth-tab ${mode === 'register' ? 'active' : ''}`}
                        onClick={() => { setMode('register'); setError(null); }}
                    >Create Account</button>
                </div>

                {error && <div className="auth-error">{error}</div>}

                <form className="auth-form" onSubmit={handleSubmit}>
                    {mode === 'register' && (
                        <div className="auth-input-group">
                            <label>Display Name</label>
                            <input 
                                type="text" 
                                value={displayName} 
                                onChange={e => setDisplayName(e.target.value)}
                                placeholder="How should we call you?"
                            />
                        </div>
                    )}
                    <div className="auth-input-group">
                        <label>Email Address</label>
                        <input 
                            type="email" 
                            required 
                            value={email} 
                            onChange={e => setEmail(e.target.value)}
                            placeholder="you@example.com"
                        />
                    </div>
                    <div className="auth-input-group">
                        <label>Password</label>
                        <input 
                            type="password" 
                            required 
                            minLength={6}
                            value={password} 
                            onChange={e => setPassword(e.target.value)}
                            placeholder="••••••••"
                        />
                    </div>
                    <button type="submit" className="auth-submit-btn" disabled={isSubmitting}>
                        {isSubmitting ? 'Please wait...' : (mode === 'login' ? 'Sign In' : 'Create Account')}
                    </button>
                </form>

                <div className="auth-divider">
                    <span>OR</span>
                </div>

                <div className="auth-google-btn">
                    <GoogleLogin
                        onSuccess={async (response) => {
                            if (response.credential) {
                                try {
                                    await loginGoogle(response.credential);
                                } catch (err) {
                                    setError(err instanceof Error ? err.message : 'Google Login Failed');
                                }
                            }
                        }}
                        onError={() => {
                            setError('Google Login Failed');
                        }}
                        theme="filled_blue"
                        size="large"
                        width="100%"
                        text={mode === 'login' ? "signin_with" : "signup_with"}
                        shape="rectangular"
                    />
                </div>
            </div>
        </div>
    );
}
