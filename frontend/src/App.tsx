import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { GoogleOAuthProvider } from '@react-oauth/google';
import { useEffect } from 'react';
import { AuthProvider } from './contexts/AuthContext';
import SearchPage from './pages/SearchPage';
import LandingPage from './pages/LandingPage';
import CollectionsPage from './pages/CollectionsPage';
import CollectionDetailPage from './pages/CollectionDetailPage';
import AuthGate from './components/AuthGate';
import { pingHealth } from './api';
import './index.css';

const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID || '';

function App() {
    useEffect(() => {
        // Ping the backend every 10 minutes to prevent Render free-tier from sleeping.
        // Doing this ensures the backend stays awake as long as the user's tab is open.
        const PING_INTERVAL = 10 * 60 * 1000;
        
        // Initial ping
        pingHealth();
        
        const intervalId = setInterval(pingHealth, PING_INTERVAL);

        return () => clearInterval(intervalId);
    }, []);

    return (
        <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
            <AuthProvider>
                <BrowserRouter>
                    <Routes>
                        <Route path="/" element={<LandingPage />} />
                        <Route path="/search" element={<SearchPage />} />
                        <Route path="/login" element={<AuthGate children={<SearchPage />} />} />
                        <Route path="/collections" element={
                            <AuthGate>
                                <CollectionsPage />
                            </AuthGate>
                        } />
                        <Route path="/collections/:id" element={
                            <AuthGate>
                                <CollectionDetailPage />
                            </AuthGate>
                        } />
                    </Routes>
                </BrowserRouter>
            </AuthProvider>
        </GoogleOAuthProvider>
    );
}

export default App;
