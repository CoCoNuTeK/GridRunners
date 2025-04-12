import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { userApi } from '../../services/rest/user/api';
import { mazeGameApi } from '../../services/rest/mazegame/api';
import { UserProfileResponse, MatchHistory, UpdateDisplayNameRequest, UpdateProfileImageRequest } from '../../services/rest/user/types';
import { RedirectOverlay } from '../../components/common/RedirectOverlay';
import './ProfilePage.scss';

const ProfilePage: React.FC = () => {
    const navigate = useNavigate();
    const { user, logout } = useAuth();
    const [profile, setProfile] = React.useState<UserProfileResponse | null>(null);
    const [isLoading, setIsLoading] = React.useState(true);
    const [error, setError] = React.useState<string | null>(null);
    const [isCriticalError, setIsCriticalError] = React.useState(false);
    const [showGameMenu, setShowGameMenu] = React.useState(false);
    const [showCreateGameModal, setShowCreateGameModal] = React.useState(false);
    const [showRedirect, setShowRedirect] = React.useState(false);
    const [redirectMessage, setRedirectMessage] = React.useState('');
    const [redirectDestination, setRedirectDestination] = React.useState('');
    const [showEditName, setShowEditName] = React.useState(false);
    const [showMatchDetails, setShowMatchDetails] = React.useState(false);
    const [selectedMatch, setSelectedMatch] = React.useState<MatchHistory | null>(null);
    const [newDisplayName, setNewDisplayName] = React.useState('');
    const [isUpdating, setIsUpdating] = React.useState(false);
    const [successMessage, setSuccessMessage] = React.useState<string | null>(null);
    const gameDataRef = React.useRef<any>(null);
    const fileInputRef = React.useRef<HTMLInputElement>(null);
    const [newGameName, setNewGameName] = React.useState('');

    React.useEffect(() => {
        loadProfile();
        
        // Check if we need to show a success message after refresh
        const showSuccessAfterRefresh = sessionStorage.getItem('displayNameUpdated');
        if (showSuccessAfterRefresh) {
            setSuccessMessage('Display name updated successfully!');
            setTimeout(() => setSuccessMessage(null), 3000);
            sessionStorage.removeItem('displayNameUpdated');
        }
    }, []);

    const loadProfile = async () => {
        try {
            setError(null); // Clear any previous errors
            setIsCriticalError(false);
            const data = await userApi.getProfile();
            setProfile(data);
            setNewDisplayName(data.displayName);
        } catch (err) {
            setError('Failed to load profile');
            setIsCriticalError(true); // Mark as critical error
            console.error('Profile load error:', err);
        } finally {
            setIsLoading(false);
        }
    };

    const handleLogout = async () => {
        try {
            await logout();
            navigate('/unauthorized', { state: { logout: true } });
        } catch (err) {
            setError('Failed to logout');
            console.error('Logout error:', err);
        }
    };

    const openCreateGameModal = () => {
        setShowGameMenu(false);
        const defaultName = `${profile?.displayName || user?.username}'s Game`;
        setNewGameName(defaultName);
        setShowCreateGameModal(true);
    };

    const handleCreateGame = async () => {
        try {
            setShowCreateGameModal(false);
            const game = await mazeGameApi.createGame({ 
                name: newGameName.trim() 
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

    const handleUpdateDisplayName = async () => {
        if (!newDisplayName.trim()) {
            setError('Display name cannot be empty');
            setTimeout(() => setError(null), 5000); // 5 seconds for validation errors
            return;
        }

        setIsUpdating(true);
        try {
            const request: UpdateDisplayNameRequest = {
                displayName: newDisplayName.trim()
            };
            await userApi.updateDisplayName(request);
            setShowEditName(false);
            
            // Set flag to show success message after refresh
            sessionStorage.setItem('displayNameUpdated', 'true');
            
            // Refresh immediately
            window.location.reload();
        } catch (err) {
            setError('Failed to update display name');
            console.error('Update display name error:', err);
            setTimeout(() => setError(null), 5000); // 5 seconds for API errors
            setIsUpdating(false);
        }
    };

    const handleProfileImageChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        if (!file) return;

        // Validate file type client-side before sending to server
        const validTypes = ['image/jpeg', 'image/jpg', 'image/png'];
        if (!validTypes.includes(file.type)) {
            setError('Only JPG, JPEG and PNG files are allowed');
            setTimeout(() => setError(null), 5000); // 5 seconds for validation errors
            
            // Reset file input
            if (fileInputRef.current) {
                fileInputRef.current.value = '';
            }
            return;
        }

        // Validate file size (max 10MB as per backend)
        if (file.size > 10 * 1024 * 1024) {
            setError('File size exceeds 10 MB limit');
            setTimeout(() => setError(null), 5000); // 5 seconds for validation errors
            
            // Reset file input
            if (fileInputRef.current) {
                fileInputRef.current.value = '';
            }
            return;
        }

        setIsUpdating(true);
        try {
            const request: UpdateProfileImageRequest = {
                imageFile: file
            };
            const updatedProfile = await userApi.updateProfileImage(request);
            setProfile(updatedProfile);
            setSuccessMessage('Profile picture updated successfully!');
            setTimeout(() => setSuccessMessage(null), 3000);
        } catch (err: any) {
            console.error('Update profile picture error:', err);
            let errorMessage = 'Failed to update profile picture';
            
            if (err.response) {
                switch (err.response.status) {
                    case 400:
                        errorMessage = err.response.data?.message || 'No file was uploaded';
                        break;
                    case 401:
                        errorMessage = 'Please log in to update your profile picture';
                        break;
                    case 415:
                        errorMessage = err.response.data?.message || 'Unsupported image format. Only JPG, JPEG and PNG files are allowed';
                        break;
                    case 500:
                        errorMessage = 'Server error. Please try again later';
                        break;
                }
            }
            
            setError(errorMessage);
            setTimeout(() => setError(null), 5000); // 5 seconds for API errors
            
            // Reload profile to ensure consistent state after error
            loadProfile();
        } finally {
            setIsUpdating(false);
            // Reset file input
            if (fileInputRef.current) {
                fileInputRef.current.value = '';
            }
        }
    };

    if (isLoading) {
        return <div className="profile-page loading">Loading profile...</div>;
    }

    if (isCriticalError && error) {
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
                        <div className="profile-image-container">
                            <div className="profile-image">
                                {profile?.profileImageUrl ? (
                                    <img src={profile.profileImageUrl} alt="Profile" />
                                ) : (
                                    <div className="default-avatar">
                                        {profile?.displayName?.[0]?.toUpperCase() || user?.username?.[0]?.toUpperCase()}
                                    </div>
                                )}
                            </div>
                            <button 
                                className="change-image-btn"
                                onClick={() => fileInputRef.current?.click()}
                                disabled={isUpdating}
                            >
                                Change Picture
                            </button>
                            <input
                                type="file"
                                ref={fileInputRef}
                                onChange={handleProfileImageChange}
                                accept="image/*"
                                style={{ display: 'none' }}
                            />
                        </div>
                        <div className="profile-details">
                            <div className="display-name-container">
                                <h2>{profile?.displayName || user?.username}</h2>
                                <button 
                                    className="edit-name-btn"
                                    onClick={() => setShowEditName(true)}
                                    disabled={isUpdating}
                                >
                                    Edit
                                </button>
                            </div>
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

                {!isCriticalError && error && (
                    <div className="error-message">
                        {error}
                    </div>
                )}

                {successMessage && (
                    <div className="success-message">
                        {successMessage}
                    </div>
                )}

                <div className="match-history">
                    <h3>Match History</h3>
                    {profile?.matchHistory && profile.matchHistory.length > 0 ? (
                        <div className="match-list">
                            {profile.matchHistory.map((match: MatchHistory) => (
                                <div 
                                    key={match.gameIdentifier} 
                                    className="match-item"
                                    onClick={() => {
                                        setSelectedMatch(match);
                                        setShowMatchDetails(true);
                                    }}
                                >
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

                {showMatchDetails && selectedMatch && (
                    <div className="match-details-modal">
                        <div className="modal-content">
                            <button className="close-button" onClick={() => setShowMatchDetails(false)}>
                                ×
                            </button>
                            <h2>{selectedMatch.name}</h2>
                            <div className="match-result-container">
                                <span className={`match-result ${selectedMatch.wonByUser ? 'win' : 'loss'}`}>
                                    {selectedMatch.wonByUser ? 'Victory' : 'Defeat'}
                                </span>
                                <span className="match-date">
                                    Ended: {new Date(selectedMatch.createdAt).toLocaleString()}
                                </span>
                            </div>
                            <div className="participants-section">
                                <h3>Participants</h3>
                                <div className="participants-list">
                                    {selectedMatch.participants.map((participant, index) => (
                                        <div key={index} className="participant">
                                            {typeof participant === 'string' ? participant : 
                                             // Handle case where participant might be an object
                                             (participant as any).displayName || (participant as any).username || 'Unknown Player'}
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    </div>
                )}

                {showGameMenu && (
                    <div className="game-menu-modal">
                        <div className="modal-content">
                            <button className="close-button" onClick={() => setShowGameMenu(false)}>
                                ×
                            </button>
                            <h2>Choose Game Mode</h2>
                            <div className="game-options">
                                <button onClick={openCreateGameModal} className="btn">
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

                {showCreateGameModal && (
                    <div className="edit-name-modal">
                        <div className="modal-content">
                            <button className="close-button" onClick={() => setShowCreateGameModal(false)}>
                                ×
                            </button>
                            <h2>Create New Game</h2>
                            <div className="input-group">
                                <label htmlFor="game-name">Game Name</label>
                                <input
                                    id="game-name"
                                    type="text"
                                    value={newGameName}
                                    onChange={(e) => setNewGameName(e.target.value)}
                                    placeholder="Enter game name"
                                    maxLength={50}
                                />
                            </div>
                            <div className="modal-actions">
                                <button 
                                    onClick={handleCreateGame}
                                    className="btn"
                                    disabled={!newGameName.trim()}
                                >
                                    Create Game
                                </button>
                                <button 
                                    onClick={() => setShowCreateGameModal(false)}
                                    className="btn secondary"
                                >
                                    Cancel
                                </button>
                            </div>
                        </div>
                    </div>
                )}

                {showEditName && (
                    <div className="edit-name-modal">
                        <div className="modal-content">
                            <button className="close-button" onClick={() => setShowEditName(false)}>
                                ×
                            </button>
                            <h2>Edit Display Name</h2>
                            <div className="input-group">
                                <input
                                    type="text"
                                    value={newDisplayName}
                                    onChange={(e) => setNewDisplayName(e.target.value)}
                                    placeholder="Enter new display name"
                                    maxLength={50}
                                />
                            </div>
                            <div className="modal-actions">
                                <button 
                                    onClick={handleUpdateDisplayName}
                                    className="btn"
                                    disabled={isUpdating || !newDisplayName.trim()}
                                >
                                    {isUpdating ? 'Updating...' : 'Save Changes'}
                                </button>
                                <button 
                                    onClick={() => setShowEditName(false)}
                                    className="btn secondary"
                                    disabled={isUpdating}
                                >
                                    Cancel
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