using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using csharp_api.Services;
using csharp_api.Services.Discord;
using csharp_api.Database.DynamoDB;
using csharp_api.Database;

namespace csharp_api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Configure CORS
            // TODO Cleanup CORS config
            services.AddCors(o => o.AddPolicy("MyPolicy", builder =>
            {
                builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            }));

            var tokenConfig = Configuration.GetSection("TokenManagement");
            var TokenSigningKey = new SymmetricSecurityKey(Encoding.Default.GetBytes(tokenConfig["SigningKey"]));

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateLifetime = true,
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidIssuer = tokenConfig["Issuer"],
                    ValidAudience = tokenConfig["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TokenSigningKey,
                    NameClaimType = "userId",
                    RoleClaimType = "accessLevel"
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireClaim("accessLevel", "admin"));
                options.AddPolicy("UserOnly", policy => policy.RequireClaim("accessLevel", "user", "admin"));
                options.AddPolicy("RegistrationOnly", policy => policy.RequireClaim("accessLevel", "registration"));
            });

            // Configure DI
            services.AddSingleton<DiscordAuthenticator, DiscordAuthenticator>();
            services.AddSingleton<IDatabase, DynamoDBContext>();
            services.AddSingleton<TokenManager, TokenManager>();
            services.AddSingleton<UserService, UserService>();
            services.AddSingleton<LobbyService, LobbyService>();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // TODO : Research exception handling and implement this better
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            // Disable https redirection for now...
            // app.UseHttpsRedirection();

            app.UseCors("MyPolicy");

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
