import { ApiError } from '../auth/types';

export interface Player {
    id: number;
    username?: string;
    displayName: string;
    profileImage?: string;
}

export interface MazeGame {
    id: number;
    name: string;
    createdAt: string;
    startedAt: string | null;
    endedAt: string | null;
    state: GameState;
    winnerId: number | null;
    botCount: number;
    players: Player[];
}

export enum GameState {
    Lobby = 'Lobby',
    InGame = 'InGame',
    Finished = 'Finished'
}

export interface CreateGameRequest {
    name: string;
}

export interface GameResponse {
    id: number;
    name: string;
    state: GameState;
    players: Player[];
    botCount: number;
    createdAt: string;
    startedAt?: string;
    finishedAt?: string;
    winnerId?: number;
}

export interface GameListResponse {
    games: GameResponse[];
}

export interface GameError {
    message: string;
    code?: string;
}

export type MazeGameError = ApiError & {
    status: 400 | 401 | 404 | 500;
}; 