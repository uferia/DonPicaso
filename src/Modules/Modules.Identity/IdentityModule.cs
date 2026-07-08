using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Auth.Login;
using Modules.Identity.Features.Auth.Logout;
using Modules.Identity.Features.Auth.Me;
using Modules.Identity.Features.Auth.Refresh;
using Modules.Identity.Features.Auth.StaffLogin;
using Modules.Identity.Features.Auth.StaffRoster;
using Modules.Identity.Features.Branches.CreateBranch;
using Modules.Identity.Features.Branches.GetBranch;
using Modules.Identity.Features.Branches.ListBranches;
using Modules.Identity.Features.Branches.SetBranchActiveState;
using Modules.Identity.Features.Branches.UpdateBranch;
using Modules.Identity.Features.Brands.CreateBrand;
using Modules.Identity.Features.Brands.GetBrand;
using Modules.Identity.Features.Brands.ListBrands;
using Modules.Identity.Features.Brands.SetBrandActiveState;
using Modules.Identity.Features.Brands.UpdateBrand;
using Modules.Identity.Features.Users;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Features.Users.GetUser;
using Modules.Identity.Features.Users.ListUsers;
using Modules.Identity.Features.Users.UpdateUser;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, string connectionString, JwtOptions jwtOptions)
    {
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));

        services.AddValidatorsFromAssembly(typeof(IdentityModule).Assembly, includeInternalTypes: true);

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<StaffLoginCommandHandler>();
        services.AddScoped<GetStaffRosterQueryHandler>();
        services.AddScoped<RefreshCommandHandler>();
        services.AddScoped<LogoutCommandHandler>();
        services.AddScoped<CreateBrandCommandHandler>();
        services.AddScoped<ListBrandsQueryHandler>();
        services.AddScoped<GetBrandQueryHandler>();
        services.AddScoped<UpdateBrandCommandHandler>();
        services.AddScoped<SetBrandActiveStateCommandHandler>();
        services.AddScoped<CreateBranchCommandHandler>();
        services.AddScoped<ListBranchesQueryHandler>();
        services.AddScoped<GetBranchQueryHandler>();
        services.AddScoped<UpdateBranchCommandHandler>();
        services.AddScoped<SetBranchActiveStateCommandHandler>();
        services.AddScoped<CreateUserCommandHandler>();
        services.AddScoped<ListUsersQueryHandler>();
        services.AddScoped<GetUserQueryHandler>();
        services.AddScoped<UpdateUserCommandHandler>();

        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddSingleton<IAuthorizationHandler, RoleAuthorizationHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // See Task 4's implementation note: without this, ASP.NET
                // Core silently remaps "sub"/"role" to long WS-* claim URIs
                // after validating the token.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                };
            });

        services.AddAuthorization(options => options.AddIdentityPolicies());

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder app)
    {
        app.MapLogin();
        app.MapStaffLogin();
        app.MapStaffRoster();
        app.MapRefresh();
        app.MapLogout();
        app.MapMe();
        app.MapCreateBrand();
        app.MapListBrands();
        app.MapGetBrand();
        app.MapUpdateBrand();
        app.MapDeactivateBrand();
        app.MapReactivateBrand();
        app.MapCreateBranch();
        app.MapListBranches();
        app.MapGetBranch();
        app.MapUpdateBranch();
        app.MapDeactivateBranch();
        app.MapReactivateBranch();
        app.MapCreateUser();
        app.MapListUsers();
        app.MapGetUser();
        app.MapUpdateUser();
        return app;
    }
}
