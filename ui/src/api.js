// All calls to the backend are here, so the pages stay simple.
const BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7120';

// A short sentence for the screen. The API sends plain text for 400, 401 and 409 and a JSON
// problem document for 503 (it cannot reach RabbitMQ), which is not something to show a player.
async function errorMessage(response) {
  if (response.status === 503) {
    return 'The bet could not be queued. RabbitMQ is probably not running. Start it and try again.';
  }

  const text = await response.text();
  try {
    const problem = JSON.parse(text);
    return problem.detail || problem.title || `Request failed (${response.status})`;
  } catch {
    return text || `Request failed (${response.status})`;
  }
}

async function request(path, options = {}) {
  const response = await fetch(BASE_URL + path, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
  });

  if (!response.ok) {
    throw new Error(await errorMessage(response));
  }

  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

export function login(loginName, password) {
  return request('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ loginName, password }),
  });
}

export function register(loginName, password) {
  return request('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify({ loginName, password }),
  });
}

export function getGames() {
  return request('/api/games');
}

// existing endpoint 1: place a bet
export function placeBet(bet) {
  return request('/api/player/casinowager', {
    method: 'POST',
    body: JSON.stringify(bet),
  });
}

// existing endpoint 2: one player's bets, newest first
export function getPlayerBets(accountId, page, pageSize) {
  return request(`/api/player/${accountId}/casino?page=${page}&pageSize=${pageSize}`);
}

// existing endpoint 3: players who spent the most
export function getTopSpenders(count) {
  return request(`/api/player/topSpenders?count=${count}`);
}

// admin only: registered players (with a login), optionally filtered by part of the login name
export function getRegisteredPlayers(search, adminToken) {
  return request(`/api/admin/players?search=${encodeURIComponent(search)}&count=50`, {
    headers: { 'X-Admin-Token': adminToken },
  });
}

// admin only: kind is "unit" or "load"
export function runTests(kind, adminToken) {
  return request(`/api/admin/tests/${kind}`, {
    method: 'POST',
    headers: { 'X-Admin-Token': adminToken },
  });
}
