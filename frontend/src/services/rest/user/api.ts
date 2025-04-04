import axiosInstance from '../axios';
import { UserProfileResponse, UpdateDisplayNameRequest, UpdateProfileImageRequest } from './types';

class UserApi {
    async getProfile(): Promise<UserProfileResponse> {
        const response = await axiosInstance.get<UserProfileResponse>('/User/profile');
        return response.data;
    }

    async updateDisplayName(request: UpdateDisplayNameRequest): Promise<UserProfileResponse> {
        const response = await axiosInstance.put<UserProfileResponse>('/User/display-name', request);
        return response.data;
    }

    async updateProfileImage(request: UpdateProfileImageRequest): Promise<UserProfileResponse> {
        const response = await axiosInstance.put<UserProfileResponse>('/User/profile-image', request);
        return response.data;
    }
}

export const userApi = new UserApi(); 