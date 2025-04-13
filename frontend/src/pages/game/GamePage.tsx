import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { signalRService } from '../../services/websocket/signalR';
import { GameState, CellType } from '../../services/websocket/types';
import { getUserIdFromToken } from '../../services/auth/jwt';
import './GamePage.scss';

const GamePage: React.FC = () => {
    const navigate = useNavigate();
    const location = useLocation();
    const [game, setGame] = React.useState<GameState | null>(null);
    const gameRef = React.useRef<GameState | null>(null);
    const [loading, setLoading] = React.useState(true);
    const [showWinnerModal, setShowWinnerModal] = React.useState(false);
    const [winner, setWinner] = React.useState<{ id: number; name: string } | null>(null);
    const [error, setError] = React.useState<string | null>(null);
    const [disconnectedPlayers, setDisconnectedPlayers] = React.useState<Set<number>>(new Set());
    const currentPositionRef = React.useRef<{x: number, y: number} | null>(null);
    const movementBlockedRef = React.useRef<boolean>(false);

    // Get player ID using useMemo to ensure it only runs once per mount
    const playerId = React.useMemo(() => {
        return getUserIdFromToken();
    }, []);

    // Update ref when game changes
    React.useEffect(() => {
        gameRef.current = game;
    }, [game]);

    // Set up event handlers and initial game state on mount
    React.useEffect(() => {
        // Prevent scrolling while in game
        document.body.style.overflow = 'hidden';
        
        // Set initial game state from location
        setGame(location.state.game);
        setLoading(false);

        // Initialize current position from playerPositions
        if (playerId && location.state.game?.playerPositions[playerId]) {
            currentPositionRef.current = location.state.game.playerPositions[playerId];
        }

        const handlePlayerMoved = (data: { playerId: number; position: { x: number; y: number } }) => {
            // Check if this is our own movement
            if (data.playerId === playerId) {
                // Update our position reference
                currentPositionRef.current = data.position;
                // Unblock movement now that server has confirmed our move
                movementBlockedRef.current = false;
            }
            
            // Update game state only once
            setGame(prevGame => {
                if (!prevGame) return prevGame;
                return {
                    ...prevGame,
                    playerPositions: {
                        ...prevGame.playerPositions,
                        [data.playerId]: data.position
                    }
                };
            });
        };

        const handlePlayerDisconnected = (data: { playerId: number; connectedPlayers: Record<number, boolean> }) => {
            setDisconnectedPlayers(prev => {
                const newSet = new Set(prev);
                newSet.add(data.playerId);
                return newSet;
            });
            setGame(prevGame => {
                if (!prevGame) return prevGame;
                return {
                    ...prevGame,
                    playerConnected: {
                        ...prevGame.playerConnected,
                        [data.playerId]: false
                    }
                };
            });
        };

        const handleGameOver = (data: { winnerId: number; finalPositions: Record<number, { x: number; y: number }> }) => {
            const winner = gameRef.current?.players.find(p => p.id === data.winnerId);
            
            // Set winner first to block movement attempts
            setWinner({
                id: data.winnerId,
                name: winner?.displayName || 'Unknown'
            });

            signalRService.stop().catch(() => {});
            
            setShowWinnerModal(true);
        };

        const handleGameError = (message: string) => {
            setError(message);
        };

        // Handle key press
        const handleKeyPress = (e: KeyboardEvent) => {
            // Ignore key repeats
            if (e.repeat) return;
            
            // Exit if game not loaded, user ID not set, movement is blocked
            if (!gameRef.current || !playerId || !currentPositionRef.current) return;
            
            // Prevent movement if winner is set
            if (winner) return;
            
            // Block rapid movement - wait for server confirmation
            if (movementBlockedRef.current) return;

            const currentPos = currentPositionRef.current;
            let newX = currentPos.x;
            let newY = currentPos.y;

            switch (e.key) {
                case 'ArrowUp':
                case 'w':
                    newY--;
                    break;
                case 'ArrowDown':
                case 's':
                    newY++;
                    break;
                case 'ArrowLeft':
                case 'a':
                    newX--;
                    break;
                case 'ArrowRight':
                case 'd':
                    newX++;
                    break;
                default:
                    return;
            }

            // Validate move before sending
            if (newX === currentPos.x && newY === currentPos.y) return;

            const dx = Math.abs(newX - currentPos.x);
            const dy = Math.abs(newY - currentPos.y);
            if (dx + dy !== 1) return;

            if (newX < 0 || newX >= gameRef.current.width || 
                newY < 0 || newY >= gameRef.current.height) return;

            if (gameRef.current.grid[newY][newX] === CellType.Wall) return;

            const isOccupied = Object.entries(gameRef.current.playerPositions)
                .some(([_, pos]) => pos.x === newX && pos.y === newY);
            if (isOccupied) return;

            // Block movement BEFORE sending the request
            movementBlockedRef.current = true;

            // Send move to server
            signalRService.movePlayer(
                gameRef.current.id, 
                newX, 
                newY,
                currentPos.x,
                currentPos.y,
                gameRef.current.grid,
                gameRef.current.playerPositions
            )
                .catch(error => {
                    // Only log critical errors
                    console.error('[GamePage] Failed to move player - Connection error');
                    movementBlockedRef.current = false;
                });
        };

        // Set up event handlers
        signalRService.on('PlayerMoved', handlePlayerMoved);
        signalRService.on('PlayerDisconnected', handlePlayerDisconnected);
        signalRService.on('GameOver', handleGameOver);
        signalRService.on('GameError', handleGameError);
        
        // Add keydown listener
        window.addEventListener('keydown', handleKeyPress);

        return () => {
            signalRService.off('PlayerMoved');
            signalRService.off('PlayerDisconnected');
            signalRService.off('PlayerReconnected');
            signalRService.off('GameOver');
            signalRService.off('GameError');
            window.removeEventListener('keydown', handleKeyPress);
            
            // Reset all refs
            movementBlockedRef.current = false;
            currentPositionRef.current = null;
            
            // Restore scrolling when component unmounts
            document.body.style.overflow = '';
        };
    }, [location.state.game, playerId, winner]);

    const getCellClass = (cell: number, x: number, y: number): string => {
        // Start with base cell class
        const classes: string[] = ['cell'];
        
        // Add type-specific class based on CellType enum
        switch (cell) {
            case CellType.Free:
                classes.push('free');
                break;
            case CellType.Wall:
                classes.push('wall');
                break;
            case CellType.Finish:
                classes.push('finish');
                break;
            default:
                break;
        }

        // Check if there's a player at this position
        const playerEntry = Object.entries(game?.playerPositions || {})
            .find(([_, pos]) => pos.x === x && pos.y === y);
        
        if (playerEntry && game) {
            const playerId = parseInt(playerEntry[0]);
            const isDisconnected = disconnectedPlayers.has(playerId);
            
            // Add player-specific classes
            classes.push('player');
            
            // Sort players by ID once
            const sortedPlayerIds = [...game.players].sort((a, b) => a.id - b.id).map(p => p.id);
            // Find position of this player in the sorted list
            const playerIndex = sortedPlayerIds.indexOf(playerId);
            
            // Assign color based on position in sorted list
            const colors = ['red', 'blue', 'green', 'purple'];
            const colorClass = colors[playerIndex];
            
            classes.push(`player-${colorClass}`);
            
            if (isDisconnected) {
                classes.push('disconnected');
            }
        }

        return classes.join(' ');
    };

    if (loading) {
        return <div className="loading">Loading game...</div>;
    }

    if (!game) {
        return <div className="error">Game not found</div>;
    }

    // Sort player IDs once for the entire component
    const sortedPlayerIds = [...game.players].sort((a, b) => a.id - b.id).map(p => p.id);
    const colors = ['red', 'blue', 'green', 'purple'];

    return (
        <div className="game-page">
            <div className="game-header">
                <h1>{game.name}</h1>
            </div>

            {error && (
                <div className="error-message">
                    {error}
                </div>
            )}

            <div className="game-content">
                <div className="player-info-bar">
                    {game.players.map((player) => {
                        const isDisconnected = disconnectedPlayers.has(player.id);
                        const playerIndex = sortedPlayerIds.indexOf(player.id);
                        const colorClass = colors[playerIndex];
                        const isCurrentPlayer = player.id === playerId;
                        
                        return (
                            <div key={player.id} className={`player-info ${isCurrentPlayer ? 'current-player' : ''}`}>
                                <div className={`player-color player-${colorClass} ${isDisconnected ? 'disconnected' : ''}`}></div>
                                <div className={`player-name ${isDisconnected ? 'disconnected' : ''}`}>
                                    {isCurrentPlayer ? 'YOU' : player.displayName}
                                </div>
                            </div>
                        );
                    })}
                </div>

                <div className="game-board">
                    {game.grid.map((row, y) => (
                        <div key={y} className="row">
                            {row.map((cell, x) => (
                                <div
                                    key={`${x}-${y}`}
                                    className={getCellClass(cell, x, y)}
                                />
                            ))}
                        </div>
                    ))}
                </div>
            </div>

            {showWinnerModal && winner && (
                <div className="winner-modal">
                    <div className="modal-content">
                        <h2>Game Over!</h2>
                        <p>{winner.name} wins!</p>
                        <div className="modal-actions">
                            <button onClick={() => navigate('/profile')}>
                                Back to Profile
                            </button>
                            <button onClick={() => navigate('/lobby/join')}>
                                Play Again
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default GamePage; 