import { useState } from 'react';
import LoginPage from './pages/LoginPage.jsx';
import PlayerPage from './pages/PlayerPage.jsx';
import AdminPage from './pages/AdminPage.jsx';
import { initialOf } from './format.js';

const STORAGE_KEY = 'ot-user';

// keeps the user logged in when the page is refreshed
function loadUser() {
  try {
    return JSON.parse(sessionStorage.getItem(STORAGE_KEY));
  } catch {
    return null;
  }
}

export default function App() {
  const [user, setUser] = useState(loadUser);

  function handleLogin(loggedInUser) {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(loggedInUser));
    setUser(loggedInUser);
  }

  function handleLogout() {
    sessionStorage.removeItem(STORAGE_KEY);
    setUser(null);
  }

  if (!user) {
    return <LoginPage onLogin={handleLogin} />;
  }

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="brand-chip" aria-hidden="true"></span>
          <h1>OT Casino</h1>
        </div>
        <div className="user-box">
          <span className="avatar" aria-hidden="true">
            {initialOf(user.username)}
          </span>
          <span className="user-text">
            <span>{user.username}</span>
            <small>{user.role === 'admin' ? 'Administrator' : 'Player'}</small>
          </span>
          <button className="button ghost small" onClick={handleLogout}>
            Log out
          </button>
        </div>
      </header>

      <main className="content">
        {user.role === 'admin' ? <AdminPage user={user} /> : <PlayerPage user={user} />}
      </main>
    </div>
  );
}
