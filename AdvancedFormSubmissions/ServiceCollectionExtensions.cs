using System;
using System.Linq;
using AdvancedFormSubmissions.Helpers.Url;
using AdvancedFormSubmissions.Models;
using EPiServer.Authorization;
using EPiServer.Forms.Core.Data;
using EPiServer.Shell.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedFormSubmissions;

public static class ServiceCollectionExtensions
{
    private static readonly Action<AuthorizationPolicyBuilder> DefaultPolicy = p => p.RequireRole(Roles.Administrators, Roles.WebAdmins, Roles.CmsAdmins);

    public static IServiceCollection AdvancedFormSubmissions(this IServiceCollection services)
    {
        return AdvancedFormSubmissions(services, DefaultPolicy);
    }

    public static IServiceCollection AdvancedFormSubmissions(this IServiceCollection services, Action<AuthorizationPolicyBuilder> configurePolicy)
    {
        services.AddScoped<IUrlService, UrlBuilder>();
        services.AddScoped<ISubmissionStorage, DdsPermanentStorage>();

        services.Configure<ProtectedModuleOptions>(
            pm =>
            {
                if (!pm.Items.Any(i => i.Name.Equals(Constants.ModuleName, StringComparison.OrdinalIgnoreCase)))
                {
                    pm.Items.Add(new ModuleDetails { Name = Constants.ModuleName });
                }
            });

        services.Configure(
            (Action<RazorViewEngineOptions>)(ro =>
            {
                if (ro.ViewLocationExpanders.Any(
                        v =>
                            v.GetType() == typeof(ModuleLocationExpander)))
                    return;
                ro.ViewLocationExpanders.Add(
                    new ModuleLocationExpander());
            }));


        services.AddAuthorization(options =>
        {
            options.AddPolicy(Constants.PolicyName, configurePolicy);
        });

        return services;
    }
}