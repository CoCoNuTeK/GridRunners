import React, { useState } from 'react';
import { BrowserRouter as Router, Routes, Route, useLocation, useNavigate } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import AuthGuard from './components/auth/AuthGuard';
import LoginPage from './pages/login/LoginPage';
import ProfilePage from './pages/profile/ProfilePage';
import JoinLobbyPage from './pages/lobby/JoinLobbyPage';
import GameLobbyPage from './pages/lobby/GameLobbyPage';
import GamePage from './pages/game/GamePage';
import { RedirectOverlay } from './components/common/RedirectOverlay';

const RedirectHandler: React.FC = () => {
    const navigate = useNavigate();
    const location = useLocation();
    const [showRedirect, setShowRedirect] = useState(true);

    // Check if this is an unauthorized redirect from AuthGuard
    const isUnauthorized = location.state?.from && location.state?.unauthorized;
    
    const handleRedirectComplete = () => {
        setShowRedirect(false);
        // Clear tokens from storage
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        navigate('/login');
    };

    return (
        <>
            {showRedirect && (
                <RedirectOverlay
                    message={isUnauthorized ? "Unauthorized Access" : "Page Not Found"}
                    destination="/login"
                    duration={1500}
                    onComplete={handleRedirectComplete}
                    type={isUnauthorized ? "error" : "error"}
                />
            )}
        </>
    );
};

const App: React.FC = () => {
    return (
        <AuthProvider>
            <Router>
                <Routes>
                    <Route path="/login" element={<LoginPage />} />
                    <Route path="/profile" element={
                        <AuthGuard>
                            <ProfilePage />
                        </AuthGuard>
                    } />
                    <Route path="/lobby/join" element={
                        <AuthGuard>
                            <JoinLobbyPage />
                        </AuthGuard>
                    } />
                    <Route path="/lobby/current" element={
                        <AuthGuard>
                            <GameLobbyPage />
                        </AuthGuard>
                    } />
                    <Route path="/game/current" element={
                        <AuthGuard>
                            <GamePage />
                        </AuthGuard>
                    } />
                    {/* Handle unauthorized access */}
                    <Route path="/unauthorized" element={<RedirectHandler />} />
                    {/* Catch-all route for 404s */}
                    <Route path="*" element={<RedirectHandler />} />
                </Routes>
            </Router>
        </AuthProvider>
    );
};

export default App;
