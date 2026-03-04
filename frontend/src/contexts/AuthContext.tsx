import { createContext, useContext, useState, useEffect, useCallback, useMemo, type ReactNode } from 'react';
import { postGoogleLogin, postManualLogin, postManualRegister, getMe, type UserDto } from '../api';

interface AuthContextType {
    user: UserDto | null;
    token: string | null;
    isLoading: boolean;
    loginGoogle: (googleIdToken: string) => Promise<void>;
    loginManual: (email: string, password: string) => Promise<void>;
    registerManual: (email: string, password: string, displayName: string) => Promise<void>;
    logout: () => void;
}

const AuthContext = createContext<AuthContextType>({
    user: null,
    token: null,
    isLoading: true,
    loginGoogle: async () => {},
    loginManual: async () => {},
    registerManual: async () => {},
    logout: () => {},
});

const TOKEN_KEY = 'modelvault_jwt';

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<UserDto | null>(null);
    const [token, setToken] = useState<string | null>(null);
    const [isLoading, setIsLoading] = useState(true);

    // Restore session from stored JWT on mount
    useEffect(() => {
        const storedToken = localStorage.getItem(TOKEN_KEY);
        if (!storedToken) {
            setIsLoading(false);
            return;
        }

        setToken(storedToken);
        getMe(storedToken)
            .then(profile => {
                setUser(profile);
            })
            .catch(() => {
                // Token expired or invalid — clear it
                localStorage.removeItem(TOKEN_KEY);
                setToken(null);
            })
            .finally(() => setIsLoading(false));
    }, []);

    const loginGoogle = useCallback(async (googleIdToken: string) => {
        const response = await postGoogleLogin(googleIdToken);
        localStorage.setItem(TOKEN_KEY, response.token);
        setToken(response.token);
        setUser(response.user);
    }, []);

    const loginManual = useCallback(async (email: string, password: string) => {
        const response = await postManualLogin(email, password);
        localStorage.setItem(TOKEN_KEY, response.token);
        setToken(response.token);
        setUser(response.user);
    }, []);

    const registerManual = useCallback(async (email: string, password: string, displayName: string) => {
        const response = await postManualRegister(email, password, displayName);
        localStorage.setItem(TOKEN_KEY, response.token);
        setToken(response.token);
        setUser(response.user);
    }, []);

    const logout = useCallback(() => {
        localStorage.removeItem(TOKEN_KEY);
        setToken(null);
        setUser(null);
    }, []);

    const contextValue = useMemo(() => ({
        user, token, isLoading, loginGoogle, loginManual, registerManual, logout
    }), [user, token, isLoading, loginGoogle, loginManual, registerManual, logout]);

    return (
        <AuthContext.Provider value={contextValue}>
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth() {
    return useContext(AuthContext);
}

export { TOKEN_KEY };
