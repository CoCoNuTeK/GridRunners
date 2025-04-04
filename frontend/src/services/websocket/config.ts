// WebSocket Configuration
export const WEBSOCKET_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:7119';

// SignalR Hub Configuration
export const SIGNALR_HUB_URL = `${WEBSOCKET_BASE_URL}/hubs/game`;

// Reconnection Configuration
export const SIGNALR_RECONNECT_INTERVALS = [0, 2000, 10000, 30000]; // Default reconnection intervals in milliseconds
export const MAX_RECONNECT_ATTEMPTS = 4; 