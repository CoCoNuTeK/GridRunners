import { ApiError } from '../auth/types';

export interface Player {
    id: number;
    username?: string;
    displayName: string;
    profileImageUrl?: string;
}

export interface MazeGame {
    id: number;
    name: string;
    createdAt: string;
    startedAt: string | null;
    endedAt: string | null;
    state: GameState;
    winnerId: number | null;
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
    createdAt: string;
    startedAt?: string;
    finishedAt?: string;
    winnerId?: number;
}

export interface GameListResponse {
    games: GameResponse[];
}

export interface DeleteGameResponse {
    message: string;
}

export interface GameError {
    message: string;
    code?: string;
}

export type MazeGameError = ApiError & {
    status: 400 | 401 | 404 | 500;
}; 