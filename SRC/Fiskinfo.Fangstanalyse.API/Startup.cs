﻿using System;
using CorrelationId;
using Fiskinfo.Fangstanalyse.API.Constants;
using Fiskinfo.Fangstanalyse.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SintefSecure.Framework.SintefSecure.AspNetCore;

namespace Fiskinfo.Fangstanalyse.API
{
    /// <summary>
    /// The main start-up class for the application.
    /// </summary>
    public class Startup : IStartup
    {
        private readonly IConfiguration configuration;
        private readonly IHostingEnvironment hostingEnvironment;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration, where key value pair settings are stored. See
        /// http://docs.asp.net/en/latest/fundamentals/configuration.html</param>
        /// <param name="hostingEnvironment">The environment the application is running under. This can be Development,
        /// Staging or Production by default. See http://docs.asp.net/en/latest/fundamentals/environments.html</param>
        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            this.configuration = configuration;
            this.hostingEnvironment = hostingEnvironment;
        }


        /// <summary>
        /// Configures the services to add to the ASP.NET Core Injection of Control (IoC) container. This method gets
        /// called by the ASP.NET runtime. See
        /// http://blogs.msdn.com/b/webdev/archive/2014/06/17/dependency-injection-in-asp-net-vnext.aspx
        /// </summary>
        public IServiceProvider ConfigureServices(IServiceCollection services) =>
            services
                .AddCorrelationIdFluent()
                .AddCustomCaching()
                .AddCustomOptions(this.configuration)
                .AddCustomRouting()
                .AddResponseCaching()
                .AddCustomResponseCompression()
                .AddCustomStrictTransportSecurity()
                .AddCustomHealthChecks()
                .AddCustomSwagger()
                .AddDbContext<FangstanalyseContext>(options => options.UseNpgsql(
                    configuration.GetConnectionString("FangstanalyseConnection")), ServiceLifetime.Scoped)
                .AddHttpContextAccessor()
                .AddVersionedApiExplorer(x => x.GroupNameFormat = "'v'VVV") // Version format: 'v'major[.minor][-status]
                // Add useful interface for accessing the ActionContext outside a controller.
                .AddSingleton<IActionContextAccessor, ActionContextAccessor>()
                // Add useful interface for accessing the IUrlHelper outside a controller.
                .AddScoped(x => x
                    .GetRequiredService<IUrlHelperFactory>()
                    .GetUrlHelper(x.GetRequiredService<IActionContextAccessor>().ActionContext))
                .AddCustomApiVersioning()
                .AddMvcCore()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddApiExplorer()
                .AddAuthorization()
                .AddDataAnnotations()
                .AddJsonFormatters()
                .AddCustomJsonOptions(this.hostingEnvironment)
                .AddCustomCors()
                .AddCustomMvcOptions()
                .Services
                .AddProjectCommands()
                .AddProjectServices()
                .BuildServiceProvider();
        

        /// <summary>
        /// Configures the application and HTTP request pipeline. Configure is called after ConfigureServices is
        /// called by the ASP.NET runtime.
        /// </summary>
        public void Configure(IApplicationBuilder application) {
            if(hostingEnvironment.IsDevelopment())
            {
                application.UseHsts()
                    .UseDeveloperErrorPages()
                    .UseCors(CorsPolicyName.AllowAny);

            } else
            {
                application.UseCors(CorsPolicyName.AllowAny);
                //application.UseCors(CorsPolicyName.AllowProd);
            }
            
            application
                // Pass a GUID in a X-Correlation-ID HTTP header to set the HttpContext.TraceIdentifier.
                // UpdateTraceIdentifier must be false due to a bug. See https://github.com/aspnet/AspNetCore/issues/5144
                .UseCorrelationId(new CorrelationIdOptions() {UpdateTraceIdentifier = false})
                .UseForwardedHeaders()
                .UseResponseCaching()
                .UseResponseCompression()
                .UseHealthChecks("/status")
                .UseHealthChecks("/status/self", new HealthCheckOptions() {Predicate = _ => false})
                .UseStaticFilesWithCacheControl()
                .UseMvc()
                .UseSwagger(
                    options => { options.PreSerializeFilters.Add((swaggerDoc, httpReq) => swaggerDoc.Host = httpReq.Host.Value); })
                .UseCustomSwaggerUI();
        }
    }
}