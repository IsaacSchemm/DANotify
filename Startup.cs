using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using ArtworkInbox.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DeviantArtFs;
using Tweetinvi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace ArtworkInbox {
    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection"),
                    o => o.EnableRetryOnFailure()));
            services.AddAuthentication()
                .AddDeviantArt(d => {
                    d.Scope.Add("feed");
                    d.ClientId = Configuration["Authentication:DeviantArt:ClientId"];
                    d.ClientSecret = Configuration["Authentication:DeviantArt:ClientSecret"];
                    d.SaveTokens = true;
                })
                .AddTwitter(t => {
                    t.ConsumerKey = Configuration["Authentication:Twitter:ConsumerKey"];
                    t.ConsumerSecret = Configuration["Authentication:Twitter:ConsumerSecret"];
                    t.SaveTokens = true;
                })
                .AddTumblr(t => {
                    t.ConsumerKey = Configuration["Authentication:Tumblr:ConsumerKey"];
                    t.ConsumerSecret = Configuration["Authentication:Tumblr:ConsumerSecret"];
                    t.SaveTokens = true;
                })
                .AddOAuth("Weasyl", "Weasyl", o => {
                    o.ClientId = "unused";
                    o.ClientSecret = "unused";
                    o.AuthorizationEndpoint = "https://weasyl-api-key-oauth2-wrapper.azurewebsites.net/api/auth";
                    o.TokenEndpoint = "https://weasyl-api-key-oauth2-wrapper.azurewebsites.net/api/token";
                    o.CallbackPath = new PathString("/signin-weasyl");

                    o.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents {
                        OnCreatingTicket = async context => {
                            if (context.Options.SaveTokens) {
                                context.Properties.StoreTokens(new[] {
                                    new AuthenticationToken { Name = "access_token", Value = context.AccessToken }
                                });
                            }

                            var creds = new WeasylFs.WeasylCredentials(context.AccessToken);
                            var user = await WeasylFs.Endpoints.Whoami.ExecuteAsync(creds);
                            context.Principal.AddIdentity(new ClaimsIdentity(new[] {
                                new Claim(ClaimTypes.NameIdentifier, $"{user.userid}"),
                                new Claim(ClaimTypes.Name, user.login),
                                new Claim("urn:weasyl:userid", $"{user.userid}"),
                                new Claim("urn:weasyl:login", user.login),
                            }));
                        },
                        OnRemoteFailure = context => {
                            context.HandleResponse();
                            context.Response.Redirect("/Home/Error?message=" + context.Failure.Message);
                            return Task.FromResult(0);
                        }
                    };
                });
            services.AddSingleton<IDeviantArtAuth>(new DeviantArtAuth(
                int.Parse(Configuration["Authentication:DeviantArt:ClientId"]),
                Configuration["Authentication:DeviantArt:ClientSecret"]));
            services.AddSingleton<IConsumerCredentials>(new ConsumerCredentials(
                Configuration["Authentication:Twitter:ConsumerKey"],
                Configuration["Authentication:Twitter:ConsumerSecret"]));
            services.AddSingleton(new ArtworkInboxTumblrClientFactory(
                Configuration["Authentication:Tumblr:ConsumerKey"],
                Configuration["Authentication:Tumblr:ConsumerSecret"]));
            services.AddDefaultIdentity<ApplicationUser>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddControllersWithViews().AddRazorRuntimeCompilation();
            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            } else {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}
