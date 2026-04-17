using JiraLib.Implementation;
using JiraLib.Interface;
using JiraLib.Models;
using Microsoft.EntityFrameworkCore;

namespace AssignmentJira
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddDbContext<TestBridgeDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Connection")));
            builder.Services.AddSingleton<IExecutionService, ExecutionService>(provide =>
            {
                // Retrieve the connection string from configuration
                var connectionString = builder.Configuration.GetConnectionString("Connection");

                // Pass the connection string to the ExecutionService constructor
                return new ExecutionService(connectionString);
            });
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Execution}/{action=Dashboard}/{id?}");
                //pattern: "{controller=User}/{action=User}/{id?}");
            app.Run();
        }
    }
}
