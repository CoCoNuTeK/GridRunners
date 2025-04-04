import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { signalRService } from '../../services/websocket/signalR';
import { GameResponse } from '../../services/rest/mazegame/types';
import { RedirectOverlay } from '../../components/common/RedirectOverlay';
import './GameLobbyPage.scss';

const GameLobbyPage: React.FC = () => {
    const navigate = useNavigate();
    const location = useLocation();
    const [game, setGame] = React.useState<GameResponse | null>(null);
    const [loading, setLoading] = React.useState(true);
    const [error, setError] = React.useState<string | null>(null);
    const [showRedirect, setShowRedirect] = React.useState(false);
    const [redirectMessage, setRedirectMessage] = React.useState('');
    const [redirectDestination, setRedirectDestination] = React.useState('');

    React.useEffect(() => {
        const gameData = location.state?.game;
        if (gameData) {
            setGame(gameData);
            
            // Initialize SignalR connection
            const initializeSignalR = async () => {
                try {
                    // Initialize SignalR connection
                    await signalRService.initialize();
                    
                    // Set up event handlers
                    signalRService.on('PlayerJoined', (data) => {
                        setGame(prevGame => {
                            if (!prevGame) return prevGame;
                            // Check if player is already in the list
                            if (prevGame.players.some(p => p.id === data.playerId)) {
                                return prevGame;
                            }
                            return {
                                ...prevGame,
                                players: [...prevGame.players, { 
                                    id: data.playerId, 
                                    displayName: data.displayName,
                                    profileImage: data.profileImageUrl
                                }]
                            };
                        });
                    });

                    signalRService.on('PlayerLeft', (data) => {
                        setGame(prevGame => {
                            if (!prevGame) return prevGame;
                            return {
                                ...prevGame,
                                players: prevGame.players.filter(p => p.id !== data.playerId)
                            };
                        });
                    });

                    signalRService.on('BotAdded', (data) => {
                        setGame(prevGame => {
                            if (!prevGame) return prevGame;
                            return {
                                ...prevGame,
                                botCount: data.botCount
                            };
                        });
                    });

                    signalRService.on('GameStarted', (data) => {
                        // Store the game state and redirect
                        setGame(prevGame => {
                            if (!prevGame) return prevGame;
                            return {
                                ...prevGame,
                                grid: data.grid,
                                playerPositions: data.playerPositions,
                                players: data.players,
                                playerColors: data.playerColors,
                                botCount: data.botCount,
                                width: data.width,
                                height: data.height
                            };
                        });
                        setRedirectMessage('Get ready!');
                        setRedirectDestination('Game Arena');
                        setShowRedirect(true);
                    });

                    signalRService.on('GameError', (message) => {
                        setError(message);
                    });

                    // Only signal the user is joining the game if there are other players
                    if (gameData.players.length > 1) {
                        await signalRService.joinGame(gameData.id);
                    }
                    
                    setLoading(false);
                } catch (error) {
                    console.error('Failed to setup SignalR:', error);
                    setError('Failed to connect to game server. Please try refreshing the page.');
                    setLoading(false);
                }
            };

            initializeSignalR();

            // Cleanup function
            return () => {
                signalRService.off('PlayerJoined');
                signalRService.off('PlayerLeft');
                signalRService.off('BotAdded');
                signalRService.off('GameStarted');
                signalRService.off('GameError');
            };
        } else {
            setError('Game data not found. Please try creating a new game.');
        }
    }, [location.state]);

    const handleAddBot = React.useCallback(async () => {
        if (!game) return;
        try {
            await signalRService.addBot(game.id);
        } catch (error) {
            console.error('Failed to add bot:', error);
            setError('Failed to add bot. Please try again.');
        }
    }, [game]);

    const handleStartGame = React.useCallback(async () => {
        if (!game) return;
        try {
            await signalRService.startGame(game.id);
        } catch (error) {
            console.error('Failed to start game:', error);
            setError('Failed to start game. Please try again.');
        }
    }, [game]);

    const handleLeaveGame = React.useCallback(async () => {
        if (!game) return;
        try {
            await signalRService.leaveLobby(game.id);
            setRedirectMessage('Leaving game...');
            setRedirectDestination('Your Profile');
            setShowRedirect(true);
        } catch (error) {
            console.error('Failed to leave game:', error);
            setError('Failed to leave game. Please try again.');
        }
    }, [game]);

    const handleRedirectComplete = () => {
        setShowRedirect(false);
        if (redirectDestination === 'Game Arena') {
            navigate(`/game/current`, { state: { game } });
        } else if (redirectDestination === 'Your Profile') {
            navigate('/profile');
        }
    };

    // Show loading only if we don't have game data yet
    if (!game) {
        return <div className="loading">Loading game...</div>;
    }

    // Show loading while SignalR is connecting
    if (loading) {
        return <div className="loading">Connecting to game server...</div>;
    }

    return (
        <>
            {showRedirect && (
                <RedirectOverlay
                    message={redirectMessage}
                    destination={redirectDestination}
                    duration={1500}
                    onComplete={handleRedirectComplete}
                    type="success"
                />
            )}
            <div className="game-lobby-page">
                <div className="page-header">
                    <h1>{game.name}</h1>
                    <button className="back-button" onClick={handleLeaveGame}>
                        Leave Game
                    </button>
                </div>

                {error && (
                    <div className="error-message">
                        {error}
                    </div>
                )}

                <div className="lobby-content">
                    <div className="players-section">
                        <h2>Players</h2>
                        <div className="players-list">
                            {game.players.map(player => (
                                <div key={player.id} className="player-card">
                                    <div className="player-avatar">
                                        {player.profileImage ? (
                                            <img src={player.profileImage} alt={player.displayName} />
                                        ) : (
                                            <div className="default-avatar">
                                                {player.displayName[0].toUpperCase()}
                                            </div>
                                        )}
                                    </div>
                                    <div className="player-info">
                                        <h3>{player.displayName}</h3>
                                    </div>
                                </div>
                            ))}
                            {Array.from({ length: game.botCount }).map((_, index) => (
                                <div key={`bot-${index}`} className="player-card bot">
                                    <div className="player-avatar">
                                        <div className="bot-avatar">🤖</div>
                                    </div>
                                    <div className="player-info">
                                        <h3>Bot {index + 1}</h3>
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>

                    <div className="lobby-actions">
                        <button
                            onClick={handleAddBot}
                            disabled={game.players.length + game.botCount >= 4}
                            className="add-bot-button"
                        >
                            Add Bot
                        </button>
                        <button
                            onClick={handleStartGame}
                            disabled={game.players.length < 2}
                            className="start-game-button"
                        >
                            Start Game
                        </button>
                    </div>
                </div>
            </div>
        </>
    );
};

export default GameLobbyPage; 