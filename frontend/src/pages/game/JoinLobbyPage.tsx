import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { signalRService } from '../../services/websocket/signalR';
import './JoinLobbyPage.scss';

export const JoinLobbyPage: React.FC = () => {
    const navigate = useNavigate();
    const [gameId, setGameId] = useState('');
    const [error, setError] = useState<string | null>(null);
    const [isJoining, setIsJoining] = useState(false);

    const handleJoinGame = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!gameId.trim()) {
            setError('Please enter a game ID');
            return;
        }

        setIsJoining(true);
        setError(null);

        try {
            await signalRService.initialize();
            const gameIdNum = parseInt(gameId);
            await signalRService.joinGame(gameIdNum);
            navigate('/lobby', { state: { game: { id: gameIdNum } } });
        } catch (err) {
            console.error('Error joining game:', err);
            setError('Failed to join the game. Please check the game ID and try again.');
        } finally {
            setIsJoining(false);
        }
    };

    return (
        <div className="join-lobby">
            <h1>Join Game</h1>
            <form onSubmit={handleJoinGame}>
                <div className="input-group">
                    <label htmlFor="gameId">Game ID</label>
                    <input
                        type="text"
                        id="gameId"
                        value={gameId}
                        onChange={(e) => setGameId(e.target.value)}
                        placeholder="Enter game ID"
                        disabled={isJoining}
                    />
                </div>
                {error && <div className="error-message">{error}</div>}
                <div className="actions">
                    <button type="submit" disabled={isJoining}>
                        {isJoining ? 'Joining...' : 'Join Game'}
                    </button>
                    <button 
                        type="button" 
                        onClick={() => navigate('/')}
                        disabled={isJoining}
                    >
                        Back to Home
                    </button>
                </div>
            </form>
        </div>
    );
}; 