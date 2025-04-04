import axiosInstance, { authAxiosInstance } from '../axios';
import { LoginRequest, LoginResponse, RefreshTokenRequest, RefreshTokenResponse } from './types';

class AuthApi {
    async login(request: LoginRequest): Promise<LoginResponse> {
        const response = await authAxiosInstance.post<LoginResponse>('/Auth/login', request);
        if (response.data.accessToken) {
            localStorage.setItem('accessToken', response.data.accessToken);
            localStorage.setItem('refreshToken', response.data.refreshToken);
            axiosInstance.defaults.headers.common['Authorization'] = `Bearer ${response.data.accessToken}`;
        }
        return response.data;
    }

    async refreshToken(request: RefreshTokenRequest): Promise<RefreshTokenResponse> {
        const response = await axiosInstance.post<RefreshTokenResponse>('/Auth/refresh', request);
        return response.data;
    }

    async logout(): Promise<void> {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        delete axiosInstance.defaults.headers.common['Authorization'];
    }
}

export const authApi = new AuthApi(); 