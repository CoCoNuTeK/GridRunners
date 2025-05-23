import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { signalRService } from '../../services/websocket/signalR';
import { GameResponse } from '../../services/rest/mazegame/types';
import { mazeGameApi } from '../../services/rest/mazegame/api';
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
    const [isStarting, setIsStarting] = React.useState(false);

    // Helper function to deduplicate players
    const deduplicatePlayers = React.useCallback((players: any[]) => {
        const uniquePlayerMap = new Map();
        players.forEach(player => {
            if (!uniquePlayerMap.has(player.id)) {
                uniquePlayerMap.set(player.id, player);
            }
        });
        return Array.from(uniquePlayerMap.values());
    }, []);

    React.useEffect(() => {
        const gameData = location.state?.game;
        if (gameData) {
            // Deduplicate players in the initial game data
            const uniquePlayers = deduplicatePlayers(gameData.players);
            setGame({
                ...gameData,
                players: uniquePlayers
            });
            
            // Initialize SignalR connection
            const initializeSignalR = async () => {
                try {
                    // Initialize SignalR connection
                    await signalRService.initialize();
                    
                    // Set up event handlers
                    signalRService.on('PlayerJoined', (data) => {
                        setGame(prevGame => {
                            if (!prevGame) return prevGame;
                            
                            // Create a new player object
                            const newPlayer = { 
                                id: data.playerId, 
                                displayName: data.displayName,
                                profileImageUrl: data.profileImageUrl
                            };
                            
                            // Add to players array and deduplicate
                            const updatedPlayers = deduplicatePlayers([...prevGame.players, newPlayer]);
                            
                            return {
                                ...prevGame,
                                players: updatedPlayers
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

                    signalRService.on('GameStarted', (data) => {
                        // Store the game state and redirect
                        setGame(prevGame => {
                            if (!prevGame) return prevGame;
                            
                            // Deduplicate the players from the server response
                            const uniquePlayers = deduplicatePlayers(data.players);
                            
                            return {
                                ...prevGame,
                                grid: data.grid,
                                playerPositions: data.playerPositions,
                                players: uniquePlayers,
                                playerColors: data.playerColors,
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
                signalRService.off('GameStarted');
                signalRService.off('GameError');
            };
        } else {
            setError('Game data not found. Please try creating a new game.');
        }
    }, [location.state]);

    const handleStartGame = React.useCallback(async () => {
        if (!game || isStarting) return;
        
        setIsStarting(true);
        try {
            await signalRService.startGame(game.id);
            // Don't reset isStarting as we want the button to remain disabled
            // The GameStarted event will trigger a redirect
        } catch (error) {
            console.error('Failed to start game:', error);
            
            // If starting the game fails, attempt to clean up by deleting the game
            try {
                await mazeGameApi.deleteGame(game.id);
                console.log('Game deleted successfully after failed start');
                setError('Failed to start game. The game has been deleted.');
                
                // Use the existing redirect mechanism
                setRedirectMessage('Redirecting to your profile...');
                setRedirectDestination('Your Profile');
                setShowRedirect(true);
            } catch (deleteError) {
                console.error('Failed to delete game after failed start:', deleteError);
                setError('Failed to start game. Please try again or leave the game.');
            }
            
            // Only reset isStarting if there was an error and we didn't redirect
            if (!showRedirect) {
                setIsStarting(false);
            }
        }
    }, [game, isStarting, showRedirect]);

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
                                        {player.profileImageUrl ? (
                                            <img src={player.profileImageUrl} alt={player.displayName} />
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
                        </div>
                    </div>

                    <div className="lobby-actions">
                        <button
                            onClick={handleStartGame}
                            disabled={game.players.length < 2 || isStarting}
                            className="start-game-button"
                        >
                            {isStarting ? 'Starting...' : 'Start Game'}
                        </button>
                    </div>
                </div>
            </div>
        </>
    );
};

export default GameLobbyPage; 