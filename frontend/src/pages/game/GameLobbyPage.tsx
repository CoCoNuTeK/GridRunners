import React, { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { signalRService } from '../../services/websocket/signalR';
import { Player } from '../../services/rest/mazegame/types';
import './GameLobbyPage.scss';

export const GameLobbyPage: React.FC = () => {
    const location = useLocation();
    const navigate = useNavigate();
    const [players, setPlayers] = useState<Player[]>([]);
    const [isHost, setIsHost] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const game = location.state?.game;
        if (!game) {
            navigate('/');
            return;
        }

        setIsHost(game.hostId === parseInt(localStorage.getItem('userId') || '0'));
        setPlayers(game.players);

        const setupSignalR = async () => {
            try {
                await signalRService.initialize();
                await signalRService.joinGame(game.id);

                signalRService.on('PlayerJoined', (data) => {
                    setPlayers(prev => [...prev, {
                        id: data.playerId,
                        displayName: data.displayName,
                        profileImage: data.profileImageUrl
                    }]);
                });

                signalRService.on('PlayerLeft', (data) => {
                    setPlayers(prev => prev.filter(p => p.id !== data.playerId));
                });

                signalRService.on('GameStarted', () => {
                    navigate('/game', { state: { game } });
                });

                signalRService.on('GameError', (message) => {
                    setError(message);
                });
            } catch (err) {
                console.error('Error setting up SignalR:', err);
                setError('Failed to connect to the game server');
            }
        };

        setupSignalR();

        return () => {
            signalRService.off('PlayerJoined');
            signalRService.off('PlayerLeft');
            signalRService.off('GameStarted');
            signalRService.off('GameError');
            signalRService.leaveLobby(game.id);
        };
    }, [location.state, navigate]);

    const handleStartGame = async () => {
        try {
            await signalRService.startGame(location.state.game.id);
        } catch (err) {
            setError('Failed to start the game');
        }
    };

    const handleLeaveLobby = async () => {
        await signalRService.leaveLobby(location.state.game.id);
        navigate('/');
    };

    if (error) {
        return (
            <div className="game-lobby">
                <div className="error-message">{error}</div>
                <button onClick={handleLeaveLobby}>Return to Home</button>
            </div>
        );
    }

    return (
        <div className="game-lobby">
            <h1>Game Lobby</h1>
            <div className="players-list">
                <h2>Players ({players.length})</h2>
                <ul>
                    {players.map(player => (
                        <li key={player.id}>
                            {player.profileImage && (
                                <img src={player.profileImage} alt={player.displayName} />
                            )}
                            <span>{player.displayName}</span>
                        </li>
                    ))}
                </ul>
            </div>
            <div className="lobby-actions">
                {isHost && (
                    <button 
                        onClick={handleStartGame}
                        disabled={players.length < 2}
                    >
                        Start Game
                    </button>
                )}
                <button onClick={handleLeaveLobby}>Leave Lobby</button>
            </div>
        </div>
    );
}; 