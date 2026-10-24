namespace OT.Assessment.App.Models
{
    /// <summary>Body of POST api/auth/register.</summary>
    public class RegisterRequest
    {
        public string LoginName { get; set; }
        public string Password { get; set; }
    }

    /// <summary>Body of POST api/auth/login.</summary>
    public class LoginRequest
    {
        public string LoginName { get; set; }
        public string Password { get; set; }
    }

    /// <summary>
    /// What the UI gets back after a successful login or registration.
    /// Role is "player" or "admin". Token is only filled for the admin.
    /// </summary>
    public class LoginResponse
    {
        public string Role { get; set; }
        public Guid AccountId { get; set; }
        public string Username { get; set; }
        public string Token { get; set; }
    }

    /// <summary>A player row as needed for checking a login.</summary>
    public class PlayerLogin
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
    }
}
