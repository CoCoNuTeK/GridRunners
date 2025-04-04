import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { authApi } from '../../services/rest/auth/api';
import { AuthError } from '../../services/rest/auth/types';
import { RedirectOverlay } from '../../components/common/RedirectOverlay';
import './LoginPage.scss';

const LoginPage: React.FC = () => {
    const navigate = useNavigate();
    const { login } = useAuth();
    const passwordRef = React.useRef<HTMLInputElement>(null);

    const [username, setUsername] = React.useState('');
    const [error, setError] = React.useState('');
    const [isLoading, setIsLoading] = React.useState(false);
    const [showRedirect, setShowRedirect] = React.useState(false);
    const [isPasswordFocused, setIsPasswordFocused] = React.useState(false);
    const [validationErrors, setValidationErrors] = React.useState<{
        username?: string;
        password?: string;
    }>({});
    const [hasAttemptedSubmit, setHasAttemptedSubmit] = React.useState(false);
    const [passwordRequirements, setPasswordRequirements] = React.useState({
        length: false,
        uppercase: false,
        lowercase: false,
        number: false
    });

    const validatePassword = (pass: string) => {
        setPasswordRequirements({
            length: pass.length >= 6 && pass.length <= 64,
            uppercase: /[A-Z]/.test(pass),
            lowercase: /[a-z]/.test(pass),
            number: /\d/.test(pass)
        });
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        setValidationErrors({});

        const password = passwordRef.current?.value || '';

        const errors: { username?: string; password?: string } = {};

        // Username validation
        if (!username) {
            errors.username = 'Username is required';
        } else if (username.length < 3 || username.length > 30) {
            errors.username = 'Username must be between 3 and 30 characters';
        } else if (!/^[a-zA-Z0-9_-]+$/.test(username)) {
            errors.username = 'Username can only contain letters, numbers, underscores, and hyphens';
        }

        // Password validation
        if (!password) {
            errors.password = 'Password is required';
        } else if (password.length < 6 || password.length > 64) {
            errors.password = 'Password must be between 6 and 64 characters';
        } else if (!/(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/.test(password)) {
            errors.password = 'Password must contain at least one uppercase letter, one lowercase letter, and one number';
        }

        if (Object.keys(errors).length > 0) {
            setValidationErrors(errors);
            setHasAttemptedSubmit(true);
            return;
        }

        setIsLoading(true);

        try {
            const response = await authApi.login({ username, password });
            await login(response.accessToken, response.refreshToken);
            setShowRedirect(true);
        } catch (err) {
            const apiError = err as AuthError;
            let errorMessage = 'An unexpected error occurred';

            switch (apiError.status) {
                case 400:
                    errorMessage = 'Invalid input. Please check your username and password.';
                    break;
                case 401:
                    errorMessage = 'Invalid username or password';
                    break;
                case 429:
                    errorMessage = 'Too many login attempts. Please try again later.';
                    break;
                case 500:
                    errorMessage = 'Server error. Please try again later.';
                    break;
                default:
                    errorMessage = apiError.detail || errorMessage;
            }

            setError(errorMessage);
        } finally {
            setIsLoading(false);
        }
    };

    const handleRedirectComplete = () => {
        setShowRedirect(false);
        navigate('/profile');
    };

    return (
        <>
            {showRedirect && (
                <RedirectOverlay
                    message="Hello there!"
                    destination="Your Profile"
                    duration={1500}
                    onComplete={handleRedirectComplete}
                    type="success"
                />
            )}
            <div className="login-page">
                <form onSubmit={handleSubmit} className="login-form">
                    <h1>Grid Runners</h1>
                    {error && <div className="error">{error}</div>}
                    <div className={`form-group ${hasAttemptedSubmit && validationErrors.username ? 'has-error' : ''}`}>
                        <input
                            type="text"
                            placeholder="Username"
                            value={username}
                            onChange={(e) => setUsername(e.target.value)}
                            required
                            disabled={isLoading}
                        />
                        {hasAttemptedSubmit && validationErrors.username && (
                            <div className="validation-error">{validationErrors.username}</div>
                        )}
                    </div>
                    <div className={`form-group ${hasAttemptedSubmit && validationErrors.password ? 'has-error' : ''}`}>
                        <input
                            ref={passwordRef}
                            type="password"
                            placeholder="Password"
                            onChange={(e) => validatePassword(e.target.value)}
                            onFocus={() => setIsPasswordFocused(true)}
                            onBlur={() => setIsPasswordFocused(false)}
                            required
                            disabled={isLoading}
                        />
                        {hasAttemptedSubmit && validationErrors.password && (
                            <div className="validation-error">{validationErrors.password}</div>
                        )}
                    </div>
                    {isPasswordFocused && (
                        <div className="password-requirements">
                            <p>Password requirements:</p>
                            <ul>
                                <li className={passwordRequirements.length ? 'valid' : 'invalid'}>
                                    Between 6 and 64 characters
                                </li>
                                <li className={passwordRequirements.uppercase ? 'valid' : 'invalid'}>
                                    At least one uppercase letter
                                </li>
                                <li className={passwordRequirements.lowercase ? 'valid' : 'invalid'}>
                                    At least one lowercase letter
                                </li>
                                <li className={passwordRequirements.number ? 'valid' : 'invalid'}>
                                    At least one number
                                </li>
                            </ul>
                        </div>
                    )}
                    <button type="submit" disabled={isLoading}>
                        {isLoading ? 'Logging in...' : 'Login'}
                    </button>
                </form>
            </div>
        </>
    );
};

export default LoginPage;
