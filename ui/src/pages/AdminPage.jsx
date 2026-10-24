import { useEffect, useState } from 'react';
import { getRegisteredPlayers, getTopSpenders } from '../api.js';
import BetsTable from '../components/BetsTable.jsx';
import TestsPanel from '../components/TestsPanel.jsx';
import { formatMoney, initialOf } from '../format.js';
import '../styles/portal.css';

// how many players the Players tab asks for (the API allows up to 100)
const PLAYERS_SHOWN = 50;

export default function AdminPage({ user }) {
  const [tab, setTab] = useState('players');

  return (
    <div>
      <nav className="tabs">
        <button className={tab === 'players' ? 'tab active' : 'tab'} onClick={() => setTab('players')}>
          Players
        </button>
        <button className={tab === 'spenders' ? 'tab active' : 'tab'} onClick={() => setTab('spenders')}>
          Top spenders
        </button>
        <button className={tab === 'tests' ? 'tab active' : 'tab'} onClick={() => setTab('tests')}>
          Tests
        </button>
      </nav>

      {tab === 'players' && <Players adminToken={user.token} />}
      {tab === 'spenders' && <TopSpenders />}
      {tab === 'tests' && (
        <section className="panel">
          <h2>Run tests</h2>
          <TestsPanel adminToken={user.token} />
        </section>
      )}
    </div>
  );
}

// A player's name with a round initial, as used in both player tables.
function PlayerName({ name }) {
  return (
    <span className="player-cell">
      <span className="avatar" aria-hidden="true">
        {initialOf(name)}
      </span>
      {name}
    </span>
  );
}

// Registered players (the ones who can log in), with a search box and their bets.
function Players({ adminToken }) {
  const [search, setSearch] = useState('');
  const [players, setPlayers] = useState(null);
  const [error, setError] = useState('');
  const [selected, setSelected] = useState(null); // the player whose bets are shown

  useEffect(() => {
    let cancelled = false;
    setError('');
    getRegisteredPlayers(search, adminToken)
      .then((data) => {
        if (!cancelled) setPlayers(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message);
      });
    return () => {
      cancelled = true;
    };
  }, [search, adminToken]);

  return (
    <div>
      <section className="panel">
        <div className="panel-header">
          <h2>Registered players</h2>
          <input
            type="search"
            placeholder="Search login name"
            aria-label="Search login name"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        {error && <p className="notice error">{error}</p>}
        {!players && !error && <p className="muted">Loading players...</p>}
        {players && players.length === 0 && <p className="muted">No player matches "{search}".</p>}

        {players && players.length > 0 && (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Player</th>
                  <th className="number">Total spent</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {players.map((player) => (
                  <tr key={player.accountId} className={selected?.accountId === player.accountId ? 'selected' : ''}>
                    <td>
                      <PlayerName name={player.username} />
                    </td>
                    <td className="number">{formatMoney(player.totalAmountSpend)}</td>
                    <td className="number">
                      <button className="button secondary small" onClick={() => setSelected(player)}>
                        View bets
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {players && players.length >= PLAYERS_SHOWN && (
          <p className="panel-note" style={{ marginTop: 14, marginBottom: 0 }}>
            Showing the first {PLAYERS_SHOWN} players. Search by login name to find others.
          </p>
        )}
      </section>

      {selected && (
        <section className="panel">
          <h2 style={{ marginBottom: 14 }}>Bets of {selected.username}</h2>
          <BetsTable accountId={selected.accountId} />
        </section>
      )}
    </div>
  );
}

function TopSpenders() {
  const [count, setCount] = useState(10);
  const [spenders, setSpenders] = useState(null);
  const [error, setError] = useState('');
  const [selected, setSelected] = useState(null); // the player whose bets are shown

  useEffect(() => {
    setError('');
    getTopSpenders(count)
      .then(setSpenders)
      .catch((err) => setError(err.message));
  }, [count]);

  return (
    <div>
      <section className="panel">
        <div className="panel-header">
          <h2>Top spenders</h2>
          <label>
            Show{' '}
            <select value={count} onChange={(e) => setCount(Number(e.target.value))}>
              <option value={10}>10</option>
              <option value={25}>25</option>
              <option value={50}>50</option>
            </select>
          </label>
        </div>

        {error && <p className="notice error">{error}</p>}
        {!spenders && !error && <p className="muted">Loading...</p>}
        {spenders && spenders.length === 0 && <p className="muted">No players have placed a bet yet.</p>}

        {spenders && spenders.length > 0 && (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Rank</th>
                  <th>Player</th>
                  <th className="number">Total spent</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {spenders.map((spender, index) => (
                  <tr key={spender.accountId} className={selected?.accountId === spender.accountId ? 'selected' : ''}>
                    <td>{index + 1}</td>
                    <td>
                      <PlayerName name={spender.username} />
                    </td>
                    <td className="number">{formatMoney(spender.totalAmountSpend)}</td>
                    <td className="number">
                      <button className="button secondary small" onClick={() => setSelected(spender)}>
                        View bets
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {selected && (
        <section className="panel">
          <h2 style={{ marginBottom: 14 }}>Bets of {selected.username}</h2>
          <BetsTable accountId={selected.accountId} />
        </section>
      )}
    </div>
  );
}
