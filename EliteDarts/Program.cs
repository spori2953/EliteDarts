using EliteDarts.Components;
using EliteDarts.Components.Account;
using EliteDarts.Data;
using EliteDarts.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace EliteDarts
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityUserAccessor>();
            builder.Services.AddScoped<IdentityRedirectManager>();
            builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
            builder.Services.AddScoped<MatchPlayService>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
                .AddIdentityCookies();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            // Identity + Roles
            builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

            builder.Services.AddScoped<TournamentService>();
            builder.Services.AddScoped<BracketService>();
            builder.Services.AddScoped<MatchResultService>();
            builder.Services.AddScoped<BoardAssignmentService>();
            builder.Services.AddScoped<ScoreService>();
            builder.Services.AddScoped<MatchStatisticsService>();
            builder.Services.AddHttpClient<EliteDarts.Services.CvIntegrationService>();
            builder.Services.AddAntiforgery();


            var app = builder.Build();

            //Admin role + admin user (csak fejlesztéshez)
            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                const string adminRole = "Admin";
                const string adminEmail = "admin@test.com";
                const string adminPassword = "Admin123!";

                if (!await roleManager.RoleExistsAsync(adminRole))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
                    if (!roleResult.Succeeded)
                        throw new Exception("Role create failed: " + string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                }

                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser is null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                    if (!createResult.Succeeded)
                        throw new Exception("Admin user create failed: " + string.Join("; ", createResult.Errors.Select(e => e.Description)));
                }

                if (!await userManager.IsInRoleAsync(adminUser, adminRole))
                {
                    var addRoleResult = await userManager.AddToRoleAsync(adminUser, adminRole);
                    if (!addRoleResult.Succeeded)
                        throw new Exception("Add role failed: " + string.Join("; ", addRoleResult.Errors.Select(e => e.Description)));
                }
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.UseAuthentication();
            app.UseAuthorization();

            // LOGIN endpoint
            app.MapPost("/account/login", async (
                [FromForm] LoginPostModel model,
                SignInManager<ApplicationUser> signInManager) =>
            {
                if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
                {
                    return Results.Redirect("/Account/Login?error=missing");
                }

                var result = await signInManager.PasswordSignInAsync(
                    model.Email,
                    model.Password,
                    isPersistent: false,
                    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    return Results.Redirect("/", permanent: false);
                }

                return Results.Redirect("/Account/Login?error=bad");
            });

            // REGISTER endpoint
            app.MapPost("/account/register", async (
                [FromForm] RegisterPostModel model,
                UserManager<ApplicationUser> userManager,
                SignInManager<ApplicationUser> signInManager) =>
            {
                if (string.IsNullOrWhiteSpace(model.Email) ||
                    string.IsNullOrWhiteSpace(model.UserName) ||
                    string.IsNullOrWhiteSpace(model.Password) ||
                    string.IsNullOrWhiteSpace(model.ConfirmPassword))
                {
                    return Results.Redirect("/Account/Register?error=missing");
                }

                if (model.Password != model.ConfirmPassword)
                {
                    return Results.Redirect("/Account/Register?error=nomatch");
                }

                var existingEmail = await userManager.FindByEmailAsync(model.Email);
                if (existingEmail is not null)
                {
                    return Results.Redirect("/Account/Register?error=exists");
                }

                var existingUserName = await userManager.FindByNameAsync(model.UserName);
                if (existingUserName is not null)
                {
                    return Results.Redirect("/Account/Register?error=userexists");
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,     // login továbbra is emaillel
                    Email = model.Email,
                    EmailConfirmed = true,
                    DisplayName = model.UserName
                };

                var result = await userManager.CreateAsync(user, model.Password);

                if (!result.Succeeded)
                {
                    var firstError = result.Errors.FirstOrDefault()?.Code ?? "failed";
                    return Results.Redirect($"/Account/Register?error={firstError}");
                }

                await signInManager.SignInAsync(user, isPersistent: false);

                return Results.Redirect("/", permanent: false);
            });

            app.MapPost("/account/forgot-password", async (
                [FromForm] Program.ForgotPasswordPostModel model,
                UserManager<ApplicationUser> userManager) =>
            {
                var user = await userManager.FindByEmailAsync(model.Email);

                if (user is null)
                    return Results.Redirect("/Account/ForgotPasswordConfirmation");

                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var link = $"/Account/ResetPassword?email={model.Email}&code={code}";

                return Results.Redirect($"/Account/ForgotPasswordConfirmation?resetLink={Uri.EscapeDataString(link)}");
            });

            app.MapPost("/account/reset-password", async (
                [FromForm] Program.ResetPasswordPostModel model,
                UserManager<ApplicationUser> userManager) =>
            {
                if (model.Password != model.ConfirmPassword)
                {
                    return Results.Redirect($"/Account/ResetPassword?email={model.Email}&code={model.Code}&error=nomatch");
                }

                var user = await userManager.FindByEmailAsync(model.Email);
                if (user is null)
                    return Results.Redirect("/Account/Login?error=bad");

                var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code));

                var result = await userManager.ResetPasswordAsync(user, decoded, model.Password);

                if (!result.Succeeded)
                {
                    return Results.Redirect($"/Account/ResetPassword?email={model.Email}&code={model.Code}&error=badcode");
                }

                return Results.Redirect("/Account/Login");
            });

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.MapAdditionalIdentityEndpoints();

            app.Run();
        }

        public class LoginPostModel
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class RegisterPostModel
        {
            public string Email { get; set; } = "";
            public string UserName { get; set; } = "";
            public string Password { get; set; } = "";
            public string ConfirmPassword { get; set; } = "";
        }

        public class ForgotPasswordPostModel
        {
            public string Email { get; set; } = "";
        }

        public class ResetPasswordPostModel
        {
            public string Email { get; set; } = "";
            public string Code { get; set; } = "";
            public string Password { get; set; } = "";
            public string ConfirmPassword { get; set; } = "";
        }
    }
}
