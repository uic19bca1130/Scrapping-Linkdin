using Azure.Messaging.ServiceBus;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using FluentValidation.Results;
using Azure;
using Microsoft.AspNetCore.Components.Forms;
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



var app = builder.Build();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}



app.UseHttpsRedirection();



var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};
app.MapPost("/sendlink", async (ServiceBusClient client, IConfiguration configuration, [FromBody] LinkdinProfile linkModel, IValidator<LinkdinProfile> validator) =>
{
    ValidationResult validationResult = validator.Validate(linkModel);
    if (!validationResult.IsValid)
    {
        var messages = new List<string>();
        foreach (ValidationFailure failure in validationResult.Errors)
        {
            messages.Add(failure.ErrorMessage);
        }
        return Results.BadRequest(messages);
        //  return Results.BadRequest(validationResult.Errors);
    }
    string queueName = configuration.GetValue<string>("QueueName");
    string partitionKey = linkModel.PartitionKey;
    string linkedInProfileLink = linkModel.ProfileId;



    await using ServiceBusSender sender = client.CreateSender(queueName);



    // Create a Service Bus message with the LinkedIn profile link
    ServiceBusMessage message = new ServiceBusMessage(linkedInProfileLink)
    {
        PartitionKey = partitionKey
    };
    try
    {
        await sender.SendMessageAsync(message);
    }
    catch (Exception)
    {
        throw;
    }
    // Send the message to the queue

    // Return a JSON response
    return Results.Ok(new { message12 = "LinkedIn profile link sent to Azure Service Bus" });
});


app.Run();