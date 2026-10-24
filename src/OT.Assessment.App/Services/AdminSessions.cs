using System.Collections.Concurrent;

namespace OT.Assessment.App.Services
{
    /// <summary>Remembers the tokens given to the admin after login (kept in memory, lost on restart).</summary>
    public class AdminSessions
    {
        private readonly ConcurrentDictionary<string, bool> _tokens = new ConcurrentDictionary<string, bool>();

        public string CreateToken()
        {
            var token = Guid.NewGuid().ToString("N");
            _tokens[token] = true;
            return token;
        }

        public bool IsValid(string token)
        {
            return !string.IsNullOrEmpty(token) && _tokens.ContainsKey(token);
        }
    }
}
