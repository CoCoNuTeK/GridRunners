import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { SIGNALR_HUB_URL, SIGNALR_RECONNECT_INTERVALS } from './config';
import { Player } from '../rest/mazegame/types';

// Event types for type safety
export interface GameEvents {
    // Lobby Events
    PlayerJoined: (data: { playerId: number; displayName: string; profileImageUrl?: string }) => void;
    PlayerLeft: (data: { playerId: number; displayName: string; remainingPlayers: Player[] }) => void;
    
    // Game Events
    GameStarted: (data: {
        grid: number[][];
        playerPositions: Record<number, { x: number; y: number }>;
        players: { id: number; displayName: string }[];
        playerColors: Record<number, string>;
        width: number;
        height: number;
    }) => void;
    
    PlayerMoved: (data: { playerId: number; position: { x: number; y: number } }) => void;
    PlayerDisconnected: (data: { playerId: number; connectedPlayers: Record<number, boolean> }) => void;
    PlayerReconnected: (data: { playerId: number; connectedPlayers: Record<number, boolean> }) => void;
    GameOver: (data: { winnerId: number; finalPositions: Record<number, { x: number; y: number }> }) => void;
    GameError: (message: string) => void;
}

class SignalRService {
    private connection: HubConnection | null = null;
    private eventHandlers: Partial<GameEvents> = {};
    private reconnectAttempts = 0;
    private isInitialized = false;
    private connectionPromise: Promise<void> | null = null;

    private createConnection(): HubConnection {
        const accessToken = localStorage.getItem('accessToken');
        if (!accessToken) {
            throw new Error('No access token available for SignalR connection');
        }

        const connection = new HubConnectionBuilder()
            .withUrl(`${SIGNALR_HUB_URL}`, {
                accessTokenFactory: () => accessToken
            })
            .withAutomaticReconnect(SIGNALR_RECONNECT_INTERVALS)
            .configureLogging(LogLevel.Information)
            .build();

        // Setup event handlers
        this.setupEventHandlers(connection);
        this.setupConnectionEvents(connection);

        return connection;
    }

    private setupConnectionEvents(connection: HubConnection) {
        connection.onreconnecting((error) => {
            console.log('SignalR Reconnecting...');
            this.reconnectAttempts++;
        });

        connection.onreconnected((connectionId) => {
            console.log('SignalR Reconnected');
            this.reconnectAttempts = 0;
        });

        connection.onclose(() => {
            console.log('SignalR Connection closed');
            this.reconnectAttempts = 0;
            this.isInitialized = false;
        });
    }

    private setupEventHandlers(connection: HubConnection) {
        // Lobby Events
        connection.on('PlayerJoined', (data) => this.eventHandlers.PlayerJoined?.(data));
        connection.on('PlayerLeft', (data) => this.eventHandlers.PlayerLeft?.(data));

        // Game Events
        connection.on('GameStarted', (data) => {
            this.eventHandlers.GameStarted?.(data);
        });
        connection.on('PlayerMoved', (data) => {
            this.eventHandlers.PlayerMoved?.(data);
        });
        connection.on('PlayerDisconnected', (data) => this.eventHandlers.PlayerDisconnected?.(data));
        connection.on('PlayerReconnected', (data) => this.eventHandlers.PlayerReconnected?.(data));
        connection.on('GameOver', (data) => this.eventHandlers.GameOver?.(data));
        connection.on('GameError', (message) => this.eventHandlers.GameError?.(message));
    }

    public async initialize(): Promise<void> {
        // If already connected, return
        if (this.isConnected()) {
            return;
        }

        // If there's an ongoing connection attempt, wait for it
        if (this.connectionPromise) {
            return this.connectionPromise;
        }

        // Create new connection promise
        this.connectionPromise = (async () => {
            try {
                this.connection = this.createConnection();
                await this.connection.start();
                console.log('SignalR Connected');
                this.isInitialized = true;
                this.reconnectAttempts = 0;
            } catch (err) {
                console.error('SignalR Connection Error');
                this.connection = null;
                this.isInitialized = false;
                throw err;
            } finally {
                this.connectionPromise = null;
            }
        })();

        return this.connectionPromise;
    }

    public async stop(): Promise<void> {
        if (!this.connection) return Promise.resolve();
        
        if (this.connection.state === 'Disconnected') {
            return Promise.resolve();
        }
        
        // Stop the connection
        await this.connection.stop();
        this.connection = null;
        this.isInitialized = false;
    }

    public isConnected(): boolean {
        return this.connection?.state === 'Connected' || false;
    }

    public on<T extends keyof GameEvents>(event: T, handler: GameEvents[T]): void {
        this.eventHandlers[event] = handler;
    }

    public off<T extends keyof GameEvents>(event: T): void {
        delete this.eventHandlers[event];
    }

    // Game Operations
    public async joinGame(gameId: number): Promise<void> {
        if (!this.isConnected()) {
            throw new Error('SignalR connection is not established');
        }
        await this.connection!.invoke('JoinGame', gameId);
    }

    public async startGame(gameId: number): Promise<void> {
        if (!this.isConnected()) {
            throw new Error('SignalR connection is not established');
        }
        await this.connection!.invoke('StartGame', gameId);
    }

    public async movePlayer(
        gameId: number, 
        x: number, 
        y: number, 
        currentX: number, 
        currentY: number,
        grid: number[][],
        playerPositions: Record<number, { x: number; y: number }>
    ): Promise<void> {
        if (!this.isConnected()) {
            throw new Error('SignalR connection is not established');
        }
        try {
            await this.connection!.invoke('MovePlayer', gameId, x, y, currentX, currentY, grid, playerPositions);
        } catch (error) {
            throw error;
        }
    }

    public async leaveLobby(gameId: number): Promise<void> {
        if (!this.isConnected()) {
            throw new Error('SignalR connection is not established');
        }
        await this.connection!.invoke('LeaveLobby', gameId);
        await this.stop();
    }
}

// Export singleton instance
export const signalRService = new SignalRService(); 