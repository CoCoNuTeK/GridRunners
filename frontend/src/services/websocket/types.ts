import { Player } from '../rest/mazegame/types';

// Lobby Events
export interface PlayerJoinedEvent {
    playerId: number;
    displayName: string;
}

export interface PlayerLeftEvent {
    playerId: number;
    displayName: string;
    remainingPlayers: Player[];
}

export interface BotAddedEvent {
    botCount: number;
    totalParticipants: number;
}

// Game Events
export interface GameStartedEvent {
    grid: number[][];
    playerPositions: Record<number, { x: number; y: number }>;
    players: { id: number; displayName: string }[];
    playerColors: Record<number, string>;
    botCount: number;
    width: number;
    height: number;
}

export interface PlayerMovedEvent {
    playerId: number;
    position: { x: number; y: number };
}

export interface PlayerDisconnectedEvent {
    playerId: number;
    connectedPlayers: Record<number, boolean>;
}

export interface PlayerReconnectedEvent {
    playerId: number;
    connectedPlayers: Record<number, boolean>;
}

export interface GameOverEvent {
    winnerId: number;
    finalPositions: Record<number, { x: number; y: number }>;
}

// Game State Types
export enum GameStateEnum {
    Lobby = 'Lobby',
    InGame = 'InGame',
    Finished = 'Finished'
}

export interface GameState {
    id: number;
    name: string;
    state: GameStateEnum;
    players: Player[];
    grid: CellType[][];
    playerPositions: Record<number, { x: number; y: number }>;
    playerColors: Record<number, PlayerColor>;
    playerConnected: Record<number, boolean>;
    botCount: number;
    width: number;
    height: number;
}

// Cell Types
export enum CellType {
    Wall = 0,
    Free = 1,
    Finish = 2
}

// Player Colors
export enum PlayerColor {
    Red = 'red',
    Blue = 'blue',
    Green = 'green',
    Purple = 'purple'
} 