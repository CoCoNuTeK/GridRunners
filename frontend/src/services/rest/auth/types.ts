export interface LoginRequest {
    username: string;
    password: string;
}

export interface LoginResponse {
    accessToken: string;
    refreshToken: string;
    accessTokenExpiresAt: string;
    username: string;
}

export interface RefreshTokenRequest {
    refreshToken: string;
}

export interface RefreshTokenResponse {
    accessToken: string;
    refreshToken: string;
    accessTokenExpiresAt: string;
    username: string;
}

export interface AuthResponse {
    accessToken: string;
    refreshToken: string;
    accessTokenExpiresAt: string;
    username: string;
}

export interface ApiError {
    type?: string;
    title?: string;
    status: number;
    detail?: string;
    instance?: string;
}

export type AuthError = ApiError & {
    status: 400 | 401 | 429 | 500;
}; 