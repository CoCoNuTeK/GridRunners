import { jwtDecode } from 'jwt-decode';

const CLAIMS = {
    NAME_IDENTIFIER: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
    NAME: 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'
} as const;

export interface JwtPayload {
    [CLAIMS.NAME_IDENTIFIER]?: string;
    [CLAIMS.NAME]?: string;
    jti?: string;
    iat?: number | string;
    exp?: number | string;
    iss?: string;
    aud?: string;
    [key: string]: any;
}

export const getUserIdFromToken = (): number | null => {
    const token = localStorage.getItem('accessToken');
    if (!token) return null;

    try {
        const decoded = jwtDecode<JwtPayload>(token);
        const userIdRaw = decoded[CLAIMS.NAME_IDENTIFIER];
        const userId = userIdRaw ? parseInt(userIdRaw) : null;
        return isNaN(userId as number) ? null : userId;
    } catch (err) {
        console.error('[JWT] Error decoding token:', err);
        return null;
    }
};
