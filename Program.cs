using System.Dynamic;
using System.Threading;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators.Digest;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
                     {
                         options.EnableTryItOutByDefault();
                     });
}

app.MapGet("/api/status", async () =>
{
    var apiBaseUrl = "http://192.168.1.40/";
    var resourcePath = "/api/v1/status"; //"/api/v1/status";
    var username = "maker";
    var password = "REDACTED";
    var apikey = "REDACTED";

    var options = new RestClientOptions(apiBaseUrl)
    {
        Authenticator = new DigestAuthenticator(username, password),
        MaxTimeout = 30000,
    };

    var client      = new RestClient(options);
    var request     = new RestRequest(resourcePath, Method.Get);
    var queryResult = await client.ExecuteAsync(request);

    // TODO(sgartner, 2024-01-25): Generate a pretty graphic showing the status of the print.

    return Results.Text(queryResult.Content, contentType: "application/json");
})
.WithName("PrinterStatus")
.WithOpenApi();

app.Run();

