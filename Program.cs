using Azure.Messaging.ServiceBus;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Scrapping_Linkdin.Models.Request;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
IConfiguration configuration = new ConfigurationBuilder()
       .SetBasePath(Directory.GetCurrentDirectory())
       .AddJsonFile("appsettings.json")
       .Build();

builder.Services.AddSingleton<ServiceBusClient>(serviceProvider =>
{
    string connectionString = configuration.GetConnectionString("ServiceBusConnection");
    return new ServiceBusClient(connectionString);
});
builder.Services.AddTransient<IValidator<LinkdinProfile>, LinkdinProfileValidator>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
var app = builder.Build();
//builder.Services.AddScoped<ConsoleAppService>();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/sendlink", async (HttpContext context, ServiceBusClient client, IConfiguration configuration, [FromBody] LinkdinProfile linkdinProfile, IValidator<LinkdinProfile> validator) =>
{
    string sendQueueName = configuration.GetValue<string>("SendQueueName");
    string receiveQueueName = configuration.GetValue<string>("ReceiveQueueName");
    string partitionKey = linkdinProfile.PartitionKey;
    string linkedInProfileLink = linkdinProfile.ProfileId;

    await using ServiceBusSender sender = client.CreateSender(sendQueueName);

    // Create a Service Bus message with the LinkedIn profile link
    ServiceBusMessage message = new ServiceBusMessage(linkedInProfileLink)
    {
        PartitionKey = partitionKey
    };
    try
    {
        // Send the message to the queue
        await sender.SendMessageAsync(message);
        await Task.Delay(5000);

    }
    catch (Exception)
    {
        throw;
    }

    ServiceBusReceivedMessage? responseMessage = null;

    await using (ServiceBusReceiver receiver = client.CreateReceiver(receiveQueueName))
    {

        while (responseMessage == null)
        {
            IEnumerable<ServiceBusReceivedMessage> receivedMessages = await receiver.ReceiveMessagesAsync(maxMessages: 100);

            if (receivedMessages.Any())
            {
                foreach (var item in receivedMessages)
                {
                    if (item.PartitionKey == partitionKey)
                    {
                        string response = item.Body.ToString();

                        // Complete the response message to remove it from the receive queue
                        await receiver.CompleteMessageAsync(item);

                        // Return the response as JSON
                        await context.Response.WriteAsJsonAsync(new { Data = response });
                        return response;
                    }
                }
            }

            // Optional: Add delay between receive attempts to avoid continuous polling
            //await Task.Delay(TimeSpan.FromSeconds(5)); // Adjust delay duration as needed
        }
    }

    // If no response with matching partition key is available, return a JSON response indicating it
    await context.Response.WriteAsJsonAsync(new { Data = "No matching response available" });
    return null;
});


app.Run();