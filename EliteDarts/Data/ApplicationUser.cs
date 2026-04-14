using Microsoft.AspNetCore.Identity;

namespace EliteDarts.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; } = "";
    }
}