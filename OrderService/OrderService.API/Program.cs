using System.Text;
using Messaging.Common.Extensions;
using Messaging.Common.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrderService.Contracts.Messaging;
using OrderService.Infrastructure.Messaging;

namespace OrderService.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Configure JWT Authentication
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]!))
                };
            });

            #region RabbitMQ Configuration
            // 1. Bind RabbitMqOptions section from appsettings.json using the Options pattern
            builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

            // 2. Extract RabbitMQ configuration settings for immediate registration
            var mq = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>()!;

            // 3. Register persistent RabbitMQ connection and channel as singletons
            builder.Services.AddRabbitMq(mq.HostName, mq.UserName, mq.Password, mq.VirtualHost);

            // 4. Register the event publisher implementation as a singleton
            builder.Services.AddSingleton<IOrderEventPublisher, RabbitMqOrderEventPublisher>();
            #endregion

            // Register Repositories and Application Services here
            // builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            // builder.Services.AddScoped<IOrderService, OrderService.Application.Services.OrderService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}