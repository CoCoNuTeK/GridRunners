import React from 'react';
import { useNavigate } from 'react-router-dom';
import { mazeGameApi } from '../../services/rest/mazegame/api';
import { GameResponse } from '../../services/rest/mazegame/types';
import { RedirectOverlay } from '../../components/common/RedirectOverlay';
import './JoinLobbyPage.scss';

const JoinLobbyPage: React.FC = () => {
    const navigate = useNavigate();
    const [games, setGames] = React.useState<GameResponse[]>([]);
    const [searchTerm, setSearchTerm] = React.useState('');
    const [loading, setLoading] = React.useState(true);
    const [error, setError] = React.useState<string | null>(null);
    const [showRedirect, setShowRedirect] = React.useState(false);
    const [redirectMessage, setRedirectMessage] = React.useState('');
    const [redirectDestination, setRedirectDestination] = React.useState('');

    React.useEffect(() => {
        loadGames();
    }, []);

    const loadGames = async () => {
        try {
            const response = await mazeGameApi.getAvailableGames();
            setGames(response.games);
        } catch (err) {
            setError('Failed to load games');
            console.error('Failed to load games:', err);
        } finally {
            setLoading(false);
        }
    };

    const filteredGames = games.filter(game =>
        game.name.toLowerCase().includes(searchTerm.toLowerCase())
    );

    const handleJoinGame = async (gameId: number) => {
        try {
            setLoading(true);
            setError(null);

            // Join the game through REST API
            const game = await mazeGameApi.joinGame(gameId);

            // Set up redirect with game data
            setRedirectMessage('Joining game...');
            setRedirectDestination('Game Lobby');
            setShowRedirect(true);
            
            // Store game data for navigation
            navigate('/lobby/current', { 
                state: { game },
                replace: false
            });
        } catch (err) {
            setError('Failed to join game');
            console.error('Failed to join game:', err);
        } finally {
            setLoading(false);
        }
    };

    const handleRedirectComplete = () => {
        setShowRedirect(false);
        navigate(redirectDestination);
    };

    const isGameFull = (game: GameResponse) => {
        return game.players.length + game.botCount >= 4;
    };

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
            <div className="join-lobby-page">
                <div className="page-header">
                    <h1>Join Game</h1>
                    <button className="back-button" onClick={() => navigate('/profile')}>
                        Back to Profile
                    </button>
                </div>

                <div className="search-section">
                    <input
                        type="text"
                        placeholder="Search games..."
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                    />
                </div>

                {error && (
                    <div className="error-message">
                        {error}
                    </div>
                )}

                {loading ? (
                    <div className="loading">Loading games...</div>
                ) : (
                    <div className="games-list">
                        {filteredGames.length === 0 ? (
                            <div className="no-games">No games available</div>
                        ) : (
                            filteredGames.map(game => (
                                <div key={game.id} className="game-card">
                                    <div className="game-info">
                                        <h3>{game.name}</h3>
                                        <p>Created by: {game.players[0]?.displayName || 'Unknown'}</p>
                                        <div className={`player-count ${isGameFull(game) ? 'full' : ''}`}>
                                            Players: {game.players.length + game.botCount}/4
                                            {game.botCount > 0 && ` (${game.botCount} bots)`}
                                        </div>
                                    </div>
                                    <button
                                        onClick={() => handleJoinGame(game.id)}
                                        className="join-button"
                                        disabled={isGameFull(game)}
                                    >
                                        {isGameFull(game) ? 'Full' : 'Join'}
                                    </button>
                                </div>
                            ))
                        )}
                    </div>
                )}
            </div>
        </>
    );
};

export default JoinLobbyPage; 