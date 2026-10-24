import { useState } from 'react';
import { runTests } from '../api.js';

// Admin only: runs the unit tests or the load test (the Tester project) on the server.
export default function TestsPanel({ adminToken }) {
  const [running, setRunning] = useState('');
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');

  async function handleRun(kind) {
    setRunning(kind);
    setResult(null);
    setError('');
    try {
      setResult(await runTests(kind, adminToken));
    } catch (err) {
      setError(err.message);
    } finally {
      setRunning('');
    }
  }

  return (
    <div>
      <p className="panel-note">
        The load test sends 7,000 bets to the API and takes about a minute. RabbitMQ, the Consumer and
        the database must be running.
      </p>

      <div className="button-row">
        <button className="button" disabled={running !== ''} onClick={() => handleRun('unit')}>
          {running === 'unit' ? 'Running unit tests...' : 'Run unit tests'}
        </button>
        <button className="button" disabled={running !== ''} onClick={() => handleRun('load')}>
          {running === 'load' ? 'Running load test...' : 'Run load test'}
        </button>
      </div>

      {running !== '' && <p className="muted">Running. This page updates when the run finishes.</p>}

      {error && <p className="notice error">{error}</p>}

      {result && (
        <div>
          <div className="result-line">
            <span className={result.passed ? 'pill passed' : 'pill failed'}>
              {result.passed ? 'Passed' : 'Failed'}
            </span>
            <span>
              {result.kind === 'unit' ? 'Unit tests' : 'Load test'} finished in {result.seconds} s (exit code{' '}
              {result.exitCode})
            </span>
          </div>
          <pre className="output">{result.output}</pre>
        </div>
      )}
    </div>
  );
}
