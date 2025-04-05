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
        try {
            const formData = new FormData();
            formData.append('imageFile', request.imageFile);
            
            // Only override the default Content-Type when sending multipart/form-data
            const response = await axiosInstance.post<UserProfileResponse>('/User/profile-image', formData, {
                headers: {
                    'Content-Type': 'multipart/form-data'
                }
            });
            return response.data;
        } catch (error) {
            // Let the calling component handle the error
            throw error;
        }
    }
}

export const userApi = new UserApi(); 