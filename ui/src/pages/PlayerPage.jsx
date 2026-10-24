import { useEffect, useState } from 'react';
import { getGames, placeBet } from '../api.js';
import BetsTable from '../components/BetsTable.jsx';
import { formatMoney } from '../format.js';
import '../styles/portal.css';

// stake chips: value and the colour of the chip
const CHIPS = [
  { value: 5, color: 'red' },
  { value: 10, color: 'blue' },
  { value: 25, color: 'green' },
  { value: 100, color: 'black' },
];

// the database holds a lot of games, so they are shown one page at a time
const GAMES_PER_PAGE = 20;

export default function PlayerPage({ user }) {
  const [games, setGames] = useState([]);
  const [filter, setFilter] = useState('');
  const [gamePage, setGamePage] = useState(1);
  const [selectedIndex, setSelectedIndex] = useState(-1);
  const [amount, setAmount] = useState('');
  const [message, setMessage] = useState(null); // { type: 'success' | 'error', text }
  const [busy, setBusy] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    getGames()
      .then(setGames)
      .catch((err) => setMessage({ type: 'error', text: err.message }));
  }, []);

  // keep each game's position in the full list, so the chosen game survives a new search
  const text = filter.trim().toLowerCase();
  const matches = games
    .map((game, index) => ({ game, index }))
    .filter(({ game }) => `${game.gameName} ${game.provider}`.toLowerCase().includes(text));
  const totalGamePages = Math.max(1, Math.ceil(matches.length / GAMES_PER_PAGE));
  const shown = matches.slice((gamePage - 1) * GAMES_PER_PAGE, gamePage * GAMES_PER_PAGE);

  const selectedGame = games[selectedIndex];

  async function handlePlaceBet(event) {
    event.preventDefault();
    setMessage(null);

    const value = Number(amount);
    if (!selectedGame) {
      setMessage({ type: 'error', text: 'Choose a game first.' });
      return;
    }
    if (!(value > 0)) {
      setMessage({ type: 'error', text: 'Enter an amount above 0.' });
      return;
    }

    setBusy(true);
    try {
      await placeBet({
        wagerId: crypto.randomUUID(),
        accountId: user.accountId,
        username: user.username,
        countryCode: 'ZA',
        gameName: selectedGame.gameName,
        provider: selectedGame.provider,
        theme: selectedGame.theme || '',
        amount: value,
        createdDateTime: new Date().toISOString(),
        numberOfBets: 1,
        duration: 0,
        transactionId: crypto.randomUUID(),
      });
      setMessage({
        type: 'success',
        text: `Bet placed: ${formatMoney(value)} on ${selectedGame.gameName}.`,
      });
      setAmount('');
      // the Consumer saves the bet a moment after the API accepts it
      setTimeout(() => setRefreshKey((key) => key + 1), 1000);
    } catch (err) {
      setMessage({ type: 'error', text: err.message });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <div className="bet-layout">
        <section className="panel">
          <div className="panel-header">
            <h2>Choose a game</h2>
            <span className="muted">{games.length} games</span>
          </div>

          <input
            className="search"
            type="search"
            placeholder="Search by game or provider, for example Starburst"
            aria-label="Search games or providers"
            value={filter}
            onChange={(e) => {
              setFilter(e.target.value);
              setGamePage(1);
            }}
          />

          <div className="games" role="radiogroup" aria-label="Games">
            {shown.map(({ game, index }) => (
              <label key={`${game.gameName}-${game.provider}`} className="game-tile">
                <input
                  type="radio"
                  name="game"
                  checked={selectedIndex === index}
                  onChange={() => setSelectedIndex(index)}
                />
                <span className="game-name">{game.gameName}</span>
                <span className="game-provider">{game.provider}</span>
                {game.theme && <span className="game-theme">{game.theme}</span>}
              </label>
            ))}
          </div>

          {games.length > 0 && matches.length === 0 && (
            <p className="panel-note">No game matches "{filter}". Try another name or provider.</p>
          )}

          {matches.length > GAMES_PER_PAGE && (
            <div className="pager">
              <button
                type="button"
                className="button secondary small"
                disabled={gamePage <= 1}
                onClick={() => setGamePage(gamePage - 1)}
              >
                Previous
              </button>
              <span>
                Page {gamePage} of {totalGamePages} ({matches.length} games)
              </span>
              <button
                type="button"
                className="button secondary small"
                disabled={gamePage >= totalGamePages}
                onClick={() => setGamePage(gamePage + 1)}
              >
                Next
              </button>
            </div>
          )}
        </section>

        <aside className="panel bet-slip">
          <h2>Bet slip</h2>

          <form onSubmit={handlePlaceBet}>
            <div className={selectedGame ? 'slip-game' : 'slip-game empty'}>
              {selectedGame ? selectedGame.gameName : 'No game chosen yet'}
            </div>

            <div className="chips" role="group" aria-label="Quick stake">
              {CHIPS.map((chip) => (
                <button
                  key={chip.value}
                  type="button"
                  className={`chip ${chip.color}`}
                  aria-pressed={Number(amount) === chip.value}
                  aria-label={`Stake ${chip.value}`}
                  onClick={() => setAmount(String(chip.value))}
                >
                  {chip.value}
                </button>
              ))}
            </div>

            <label className="amount">
              Amount
              <input
                type="number"
                min="0.01"
                step="0.01"
                placeholder="0.00"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
            </label>

            <button className="button" type="submit" disabled={busy}>
              {busy ? 'Placing bet...' : 'Place bet'}
            </button>
          </form>

          {message && <p className={`notice ${message.type}`}>{message.text}</p>}
        </aside>
      </div>

      <section className="panel" style={{ marginTop: 20 }}>
        <div className="panel-header">
          <h2>My bets</h2>
          <button className="button secondary small" onClick={() => setRefreshKey((key) => key + 1)}>
            Refresh
          </button>
        </div>
        <BetsTable accountId={user.accountId} refreshKey={refreshKey} />
      </section>
    </div>
  );
}
