import { PlayerColor } from '../services/websocket/types';

// Map backend colors to our theme colors
export const playerColorMap: Record<PlayerColor, string> = {
    [PlayerColor.Red]: '#FF3366',    // Our primary pink
    [PlayerColor.Blue]: '#00E5FF',   // Bright cyan
    [PlayerColor.Green]: '#00FFB2',  // Our secondary neon green
    [PlayerColor.Purple]: '#B366FF'  // Vibrant purple
};

// Get CSS variables for player colors
export const getPlayerColorCSS = (color: PlayerColor): string => {
    switch (color) {
        case PlayerColor.Red:
            return 'red';
        case PlayerColor.Blue:
            return 'blue';
        case PlayerColor.Green:
            return 'green';
        case PlayerColor.Purple:
            return 'purple';
    }
};

// Get neon glow effect for player colors
export const getPlayerGlowCSS = (color: PlayerColor): string => {
    const baseColor = playerColorMap[color];
    return `0 0 10px ${baseColor}80,
            0 0 20px ${baseColor}40,
            0 0 30px ${baseColor}20`;
}; 