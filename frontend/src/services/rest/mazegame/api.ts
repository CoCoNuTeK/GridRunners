import axiosInstance from '../axios';
import { CreateGameRequest, GameResponse, GameListResponse, DeleteGameResponse } from './types';

class MazeGameApi {
    async createGame(request: CreateGameRequest): Promise<GameResponse> {
        const response = await axiosInstance.post<GameResponse>('/MazeGame', request);
        return response.data;
    }

    async getAvailableGames(): Promise<GameListResponse> {
        const response = await axiosInstance.get<GameListResponse>('/MazeGame/available');
        return response.data;
    }

    async joinGame(gameId: number): Promise<GameResponse> {
        const response = await axiosInstance.post<GameResponse>(`/MazeGame/${gameId}/join`);
        return response.data;
    }

    async deleteGame(gameId: number): Promise<DeleteGameResponse> {
        const response = await axiosInstance.delete<DeleteGameResponse>(`/MazeGame/${gameId}`);
        return response.data;
    }
}

export const mazeGameApi = new MazeGameApi(); 