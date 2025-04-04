import { ApiError } from '../auth/types';
import { GameState } from '../mazegame/types';

export interface UserProfile {
    username: string;
    displayName: string;
    profileImageUrl: string;
}

export interface MatchHistory {
    gameIdentifier: string;
    name: string;
    createdAt: string;
    participants: Player[];
    wonByUser: boolean;
}

export interface Player {
    id: number;
    username: string;
    displayName: string;
}

export interface UserProfileResponse {
    username: string;
    displayName: string;
    profileImageUrl?: string;
    matchHistory: MatchHistory[];
}

export interface UpdateDisplayNameRequest {
    displayName: string;
}

export interface UpdateProfileImageRequest {
    imageFile: File;
}

export interface UpdateDisplayNameResponse extends UserProfile {}

export interface UpdateProfileImageResponse extends UserProfile {}

export type UserError = ApiError & {
    status: 400 | 401 | 404 | 500;
}; 