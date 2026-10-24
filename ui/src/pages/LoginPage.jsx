import { useState } from 'react';
import { login, register } from '../api.js';
import '../styles/login.css';

export default function LoginPage({ onLogin }) {
  const [isRegister, setIsRegister] = useState(false);
  const [loginName, setLoginName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setBusy(true);
    try {
      const user = isRegister
        ? await register(loginName, password)
        : await login(loginName, password);
      onLogin(user);
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <aside className="login-brand">
        <div className="chip-stack" aria-hidden="true">
          <span className="red"></span>
          <span className="blue"></span>
          <span className="green"></span>
          <span className="black"></span>
        </div>
        <h1>OT Casino</h1>
        <p>Pick a game, place your bet and watch it land in your history.</p>
      </aside>

      <main className="login-form-side">
        <form className="login-card" onSubmit={handleSubmit}>
          <h2>{isRegister ? 'Create your account' : 'Log in'}</h2>

          <label>
            Login name
            <input value={loginName} onChange={(e) => setLoginName(e.target.value)} autoFocus />
          </label>

          <label>
            Password
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </label>

          {error && <p className="notice error">{error}</p>}

          <button className="button" type="submit" disabled={busy}>
            {busy ? 'Please wait...' : isRegister ? 'Create account' : 'Log in'}
          </button>

          <button
            type="button"
            className="link"
            onClick={() => {
              setIsRegister(!isRegister);
              setError('');
            }}
          >
            {isRegister ? 'I already have an account' : 'New player? Create an account'}
          </button>
        </form>
      </main>
    </div>
  );
}
