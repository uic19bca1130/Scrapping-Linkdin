using AspNetCoreRateLimit;
using Azure.Messaging.ServiceBus;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Scrapping_Linkdin.Models.Request;

namespace Scrapping_Linkdin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = CreateHostBuilder(args);
            var host = builder.Build();

            // Start the host
            host.Run();
        }



        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Add services
                    services.AddEndpointsApiExplorer();
                    services.AddSwaggerGen();
                    services.AddMemoryCache();
                    services.Configure<IpRateLimitOptions>(hostContext.Configuration.GetSection("IpRateLimiting"));
                    services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
                    services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
                    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
                    services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
                    services.AddSingleton<ServiceBusClient>(serviceProvider =>
                    {
                        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                        string connectionString = configuration.GetConnectionString("ServiceBusConnection");
                        return new ServiceBusClient(connectionString);
                    });
                    services.AddTransient<IValidator<LinkdinProfile>, LinkdinProfileValidator>();
                    services.AddValidatorsFromAssemblyContaining<Program>();
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }
    }

    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ServiceBusClient client)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/error");// This line sets up an exception handler that will catch any unhandled exceptions  during the processing of a request
                app.UseHsts();
            }
            app.UseHttpsRedirection(); //This line sets up a middleware that automatically redirects HTTP requests to HTTPS 

            app.UseRouting();   

            app.UseAuthorization();

            // Apply rate limiting middleware
            app.UseIpRateLimiting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            
            // Create background task to receive Service Bus messages
            Task.Run(() => ReceiveServiceBusMessages(client));
        }

        private async Task ReceiveServiceBusMessages(ServiceBusClient client)
        {
            string receiveQueueName = Configuration.GetValue<string>("ReceiveQueueName")!;
            var partitionKey = Guid.NewGuid().ToString();

            await using (ServiceBusReceiver receiver = client.CreateReceiver(receiveQueueName))
            {
                while (true)
                {
                    var receivedMessages = await receiver.ReceiveMessagesAsync(maxMessages: 100);

                    if (receivedMessages.Any())
                    {
                        foreach (var message in receivedMessages)
                        {
                            if (message.PartitionKey == partitionKey)
                            {
                                string response = message.Body.ToString();
                                await receiver.CompleteMessageAsync(message);

                                // Do something with the response
                                Console.WriteLine($"Received message: {response}");
                            }
                        }
                    }
                    else
                    {
                        // No messages received, wait for a short duration
                        await Task.Delay(100);
                    }
                }
            }
        }
    }
    public class LinkdinProfileController : ControllerBase
    {
        private readonly ServiceBusClient _client;
        private readonly IConfiguration _configuration;
        private readonly IValidator<LinkdinProfile> _validator;

        public LinkdinProfileController(ServiceBusClient client, IConfiguration configuration, IValidator<LinkdinProfile> validator)
        {
            _client = client;
            _configuration = configuration;
            _validator = validator;
        }

        [HttpPost("sendlink")]
        public async Task<IActionResult> SendLink([FromBody] LinkdinProfile linkdinProfile)
        {
            var validationResults = _validator.Validate(linkdinProfile);

            if (!validationResults.IsValid)
            {
                return BadRequest(validationResults.Errors);
            }

            string sendQueueName = _configuration.GetValue<string>("SendQueueName")!;
            var partitionKey = Guid.NewGuid().ToString();
            string linkedInProfileLink = linkdinProfile.ProfileId;

            await using (ServiceBusSender sender = _client.CreateSender(sendQueueName))
            {
                ServiceBusMessage message = new ServiceBusMessage(linkedInProfileLink)
                {
                    PartitionKey = partitionKey
                };

                try
                {
                    await sender.SendMessageAsync(message);
                    await Task.Delay(8000);
                }
                catch (Exception)
                {
                    throw;
                }
            }
            return Ok();
             

            //Endpoint to test the rate limiter
        }
        [HttpGet("test")]
        public IActionResult TestEndpoint()
        {

            return Ok("This is a test endpoint.");
        }
        [HttpGet("MYAPI")]
        public IActionResult MYAPIEndpoint2()
        {
            return Ok("This is MYAPI..");
        }
        [HttpGet("YOURAPI")]
        public IActionResult YOURAPIEndpoint2()
        {
            return Ok("This is YOURAPI..");
        }

    }
}
