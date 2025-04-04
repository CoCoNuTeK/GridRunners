import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { userApi } from '../../services/rest/user/api';
import { mazeGameApi } from '../../services/rest/mazegame/api';
import { UserProfileResponse, MatchHistory } from '../../services/rest/user/types';
import { RedirectOverlay } from '../../components/common/RedirectOverlay';
import './ProfilePage.scss';

const ProfilePage: React.FC = () => {
    const navigate = useNavigate();
    const { user, logout } = useAuth();
    const [profile, setProfile] = React.useState<UserProfileResponse | null>(null);
    const [isLoading, setIsLoading] = React.useState(true);
    const [error, setError] = React.useState<string | null>(null);
    const [showGameMenu, setShowGameMenu] = React.useState(false);
    const [showRedirect, setShowRedirect] = React.useState(false);
    const [redirectMessage, setRedirectMessage] = React.useState('');
    const [redirectDestination, setRedirectDestination] = React.useState('');
    const gameDataRef = React.useRef<any>(null);

    React.useEffect(() => {
        loadProfile();
    }, []);

    const loadProfile = async () => {
        try {
            const data = await userApi.getProfile();
            setProfile(data);
        } catch (err) {
            setError('Failed to load profile');
            console.error('Profile load error:', err);
        } finally {
            setIsLoading(false);
        }
    };

    const handleLogout = async () => {
        try {
            await logout();
            setRedirectMessage('See you soon!');
            setRedirectDestination('Login Page');
            setShowRedirect(true);
        } catch (err) {
            setError('Failed to logout');
            console.error('Logout error:', err);
        }
    };

    const handleCreateGame = async () => {
        try {
            setShowGameMenu(false); // Close the modal immediately
            const game = await mazeGameApi.createGame({ 
                name: `${user?.username}'s Game` 
            });
            
            setRedirectMessage('Creating your game...');
            setRedirectDestination('Game Lobby');
            setShowRedirect(true);
            gameDataRef.current = game;
        } catch (err) {
            setError('Failed to create game');
            console.error('Game creation error:', err);
        }
    };

    const handleRedirectComplete = () => {
        setShowRedirect(false);
        if (gameDataRef.current) {
            navigate('/lobby/current', { 
                state: { game: gameDataRef.current },
                replace: false
            });
        } else {
            navigate('/login');
        }
    };

    if (isLoading) {
        return <div className="profile-page loading">Loading profile...</div>;
    }

    if (error) {
        return <div className="profile-page error">{error}</div>;
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
            <div className="profile-page">
                <div className="profile-header">
                    <div className="profile-info">
                        <div className="profile-image">
                            {profile?.profileImageUrl ? (
                                <img src={profile.profileImageUrl} alt="Profile" />
                            ) : (
                                <div className="default-avatar">
                                    {profile?.displayName?.[0]?.toUpperCase() || user?.username?.[0]?.toUpperCase()}
                                </div>
                            )}
                        </div>
                        <div className="profile-details">
                            <h2>{profile?.displayName || user?.username}</h2>
                            <p className="username">@{user?.username}</p>
                        </div>
                    </div>
                    <div className="profile-actions">
                        <button onClick={() => setShowGameMenu(true)} className="btn play-button">
                            Play
                        </button>
                        <button onClick={handleLogout} className="btn logout-button">
                            Logout
                        </button>
                    </div>
                </div>

                <div className="match-history">
                    <h3>Match History</h3>
                    {profile?.matchHistory && profile.matchHistory.length > 0 ? (
                        <div className="match-list">
                            {profile.matchHistory.map((match: MatchHistory) => (
                                <div key={match.gameIdentifier} className="match-item">
                                    <div className="match-info">
                                        <span className="match-name">{match.name}</span>
                                        <span className={`match-result ${match.wonByUser ? 'win' : 'loss'}`}>
                                            {match.wonByUser ? 'Victory' : 'Defeat'}
                                        </span>
                                    </div>
                                    <div className="match-details">
                                        <span>{new Date(match.createdAt).toLocaleDateString()}</span>
                                        <span>{match.participants.length} players</span>
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <p className="no-matches">No matches played yet</p>
                    )}
                </div>

                {showGameMenu && (
                    <div className="game-menu-modal">
                        <div className="modal-content">
                            <button className="close-button" onClick={() => setShowGameMenu(false)}>
                                ×
                            </button>
                            <h2>Choose Game Mode</h2>
                            <div className="game-options">
                                <button onClick={handleCreateGame} className="btn">
                                    Create Game
                                </button>
                                <button onClick={() => {
                                    setShowGameMenu(false);
                                    navigate('/lobby/join');
                                }} className="btn secondary">
                                    Join Game
                                </button>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </>
    );
};

export default ProfilePage; 