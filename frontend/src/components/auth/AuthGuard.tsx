import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

interface AuthGuardProps {
    children: React.ReactNode;
}

const AuthGuard: React.FC<AuthGuardProps> = ({ children }) => {
    const { isAuthenticated, isLoading } = useAuth();
    const location = useLocation();

    if (isLoading) {
        return <div>Loading...</div>; // Or your loading component
    }

    if (!isAuthenticated) {
        // Redirect to catch-all route with unauthorized state
        return <Navigate 
            to="/unauthorized" 
            replace 
            state={{ 
                from: location.pathname,
                unauthorized: true 
            }} 
        />;
    }

    return <>{children}</>;
};

export default AuthGuard; 