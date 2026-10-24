import { useEffect, useState } from 'react';
import { getPlayerBets } from '../api.js';
import { formatMoney } from '../format.js';

const PAGE_SIZE = 10;

// Shows one player's bets (existing endpoint 2). Change refreshKey to reload the table.
export default function BetsTable({ accountId, refreshKey = 0 }) {
  const [page, setPage] = useState(1);
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');

  // a different player starts again from page 1
  useEffect(() => {
    setPage(1);
  }, [accountId]);

  useEffect(() => {
    let cancelled = false;
    setError('');
    getPlayerBets(accountId, page, PAGE_SIZE)
      .then((data) => {
        if (!cancelled) setResult(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message);
      });
    return () => {
      cancelled = true;
    };
  }, [accountId, page, refreshKey]);

  if (error) return <p className="notice error">{error}</p>;
  if (!result) return <p className="muted">Loading bets...</p>;
  if (result.data.length === 0) {
    return <p className="muted">No bets yet. Choose a game and place the first one.</p>;
  }

  return (
    <div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Game</th>
              <th>Provider</th>
              <th className="number">Amount</th>
              <th>Date</th>
            </tr>
          </thead>
          <tbody>
            {result.data.map((bet) => (
              <tr key={bet.wagerId}>
                <td>{bet.game}</td>
                <td>{bet.provider}</td>
                <td className="number">{formatMoney(bet.amount)}</td>
                <td>{new Date(bet.createdDate).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="pager">
        <button className="button secondary small" disabled={page <= 1} onClick={() => setPage(page - 1)}>
          Previous
        </button>
        <span>
          Page {result.page} of {result.totalPages} ({result.total} bets)
        </span>
        <button
          className="button secondary small"
          disabled={page >= result.totalPages}
          onClick={() => setPage(page + 1)}
        >
          Next
        </button>
      </div>
    </div>
  );
}
