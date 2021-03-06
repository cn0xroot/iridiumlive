/*
 * microp11 2019
 * 
 * This file is part of IridiumLive.
 * 
 * IridiumLive is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * IridiumLive is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with IridiumLive.  If not, see <http://www.gnu.org/licenses/>.
 *
 * 
 */

using BlazorUdp.Udp;
using IridiumLive.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;

namespace IridiumLive
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        private AppService rxLineService;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();

            services.AddSingleton<HttpClient>();
            services.AddSingleton<AppSettingsService>();
            services.AddDbContext<ServiceDbContext>(options =>
                   options.UseSqlite(Configuration.GetConnectionString("Sqlite")));

            services.AddServerSideBlazor().AddCircuitOptions(o => o.DetailedErrors = true);
            services.AddLocalization();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });

            var scope = app.ApplicationServices.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            try
            {
                var context = scope.ServiceProvider.GetService<ServiceDbContext>();
                //context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
                rxLineService = new AppService(context);

                int port = int.TryParse(Configuration["IridiumLiveSettings:UdpListeningPort"], out int i) ? i : 15007;
                UdpReceiver srv = new UdpReceiver(port);
                srv.OnUDPPacket += Srv_OnUDPPacket;
                srv.Start();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during system startup:configuration.");
            }
        }

        private async void Srv_OnUDPPacket(object sender, UdpPacketArgs e)
        {
            var rxLine = Encoding.UTF8.GetString(e.UdpPacket, 0, e.UdpPacket.Length);
            if (rxLine == null)
            {
                return;
            }
            await rxLineService.AddRxLine(rxLine);
        }
    }
}
