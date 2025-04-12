import React, { useEffect } from 'react';
import './RedirectOverlay.scss';

interface RedirectOverlayProps {
    message: string;
    destination: string;
    duration?: number;
    onComplete: () => void;
    type?: 'success' | 'error' | 'logout';
}

export const RedirectOverlay: React.FC<RedirectOverlayProps> = ({
    message,
    destination,
    duration = 1500,
    onComplete,
    type = 'success'
}) => {
    useEffect(() => {
        const timer = setTimeout(() => {
            onComplete();
        }, duration);

        return () => clearTimeout(timer);
    }, [duration, onComplete, message, type]);

    return (
        <div className={`redirect-overlay ${type}`}>
            <div className="redirect-content">
                <h2>{message}</h2>
                <div className="redirect-destination">
                    <span>Redirecting to</span>
                    <span className="destination">{destination}</span>
                </div>
                <div className="redirect-progress"></div>
            </div>
        </div>
    );
}; 