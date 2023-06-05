using Azure.Messaging.ServiceBus;

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
var app = builder.Build();

//Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.MapPost("/sendlink", async (ServiceBusClient client, IConfiguration configuration) =>
{
    string queueName = configuration.GetValue<string>("QueueName");

    await using ServiceBusSender sender = client.CreateSender(queueName);

    string linkedInProfileLink = "https://www.linkedin.com/in/dilbag-boparai/";

    // Create a Service Bus message with the LinkedIn profile link
    ServiceBusMessage message = new ServiceBusMessage(linkedInProfileLink);

    // Send the message to the queue
    await sender.SendMessageAsync(message);

    // Return a JSON response
    return new { Message = "LinkedIn profile link sent to Azure Service Bus" };
     
});
app.MapGet("/getresponse", async (ServiceBusClient client, IConfiguration configuration) =>
{
    string queueName = configuration.GetValue<string>("ReceiveName");

    await using ServiceBusReceiver receiver = client.CreateReceiver(queueName);

    ServiceBusReceivedMessage responseMessage = await receiver.ReceiveMessageAsync();                 
    if (responseMessage != null)
    {
        string response = responseMessage.Body.ToString();
        // Complete the response message to remove it from the queue
        //await receiver.CompleteMessageAsync(responseMessage);

        // Return the response as JSON
        return new { Data = response };
    }
    else
    {
        return new { Data = "No response available" };
    }
});

app.Run();
