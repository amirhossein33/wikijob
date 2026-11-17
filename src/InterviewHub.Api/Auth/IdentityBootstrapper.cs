using InterviewHub.Business.Auth;
using InterviewHub.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace InterviewHub.Api.Auth;

public static class IdentityBootstrapper
{
    // Role creation is idempotent; admin credentials must come from environment or user secrets.
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
            if (!await roles.RoleExistsAsync(role))
                Check(await roles.CreateAsync(new IdentityRole<Guid>(role)));

        var name = configuration["Admin:UserName"];
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
            return;

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = await users.FindByNameAsync(name);
        if (admin is null)
        {
            admin = new AppUser { UserName = name, Email = configuration["Admin:Email"] ?? "admin@interviewhub.local" };
            Check(await users.CreateAsync(admin, password));
        }
        else if (!await users.CheckPasswordAsync(admin, password))
        {
            // The configured secret is authoritative, allowing safe credential rotation.
            var resetToken = await users.GeneratePasswordResetTokenAsync(admin);
            Check(await users.ResetPasswordAsync(admin, resetToken, password));
        }
        if (!await users.IsInRoleAsync(admin, AppRoles.Admin))
            Check(await users.AddToRoleAsync(admin, AppRoles.Admin));
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
