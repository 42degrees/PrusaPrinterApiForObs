using System.Drawing;
using System.Drawing.Imaging;
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

var apiBaseUrl = "http://192.168.1.40/";
var statusPath = "/api/v1/status";
var jobPath    = "/api/v1/job";
var username   = "maker";
var password   = "REDACTED";
var apikey     = "REDACTED";

app.MapGet("/api/status", async () =>
{
    var options = new RestClientOptions(apiBaseUrl)
    {
        Authenticator = new DigestAuthenticator(username, password),
        MaxTimeout = 30000,
    };

    var client      = new RestClient(options);
    var request     = new RestRequest(statusPath, Method.Get);
    var queryResult = await client.ExecuteAsync(request);

    return Results.Text(queryResult.Content, contentType: "application/json");
})
.WithName("PrinterStatus")
.WithOpenApi();

app.MapGet("/api/statusImage", async () =>
{
    var options = new RestClientOptions(apiBaseUrl)
    {
        Authenticator = new DigestAuthenticator(username, password),
        MaxTimeout = 30000,
    };

    var client = new RestClient(options);
    var request = new RestRequest(jobPath, Method.Get);
    var queryResult = await client.ExecuteAsync(request);

    if (!queryResult.IsSuccessful)
    {
        return Results.Problem("Failed to fetch status from API.");
    }

    // Parse the JSON response
    dynamic jsonResponse = JsonConvert.DeserializeObject(queryResult.Content);
    double progress = jsonResponse.progress;
    int timeRemaining = jsonResponse.time_remaining;
    int timePrinting = jsonResponse.time_printing;
    string displayName = jsonResponse.display_name;
    string imageUrl = jsonResponse.file.refs.thumbnail;

    // Generate the image
    using var image = new Bitmap(400, 100); // Image size can be adjusted
    using var graphics = Graphics.FromImage(image);
    graphics.FillRectangle(Brushes.White, 0, 0, image.Width, image.Height); // Background

    // Draw progress bar
    var progressBarBrush = Brushes.Green;
    var progressWidth = (int)(progress / 100 * (image.Width - 20)); // Calculate width based on progress
    graphics.FillRectangle(progressBarBrush, 10, 10, progressWidth, 20);

    // Draw percentage text
    var percentageText = $"{progress}%";
    var font = new Font("Arial", 10);
    var textSize = graphics.MeasureString(percentageText, font);
    var textX = 10 + (progressWidth / 2) - (textSize.Width / 2); // Center the text in the progress bar
    var textY = 10 + (20 / 2) - (textSize.Height / 2); // Vertically center the text in the progress bar
    graphics.DrawString(percentageText, font, Brushes.Black, new PointF(textX, textY));

    // Draw time texts
    font = new Font("Arial", 12);
    var timeRemainingText = $"Time Remaining: {TimeSpan.FromSeconds(timeRemaining):hh\\:mm\\:ss}";
    var timePrintingText = $"Time Printing: {TimeSpan.FromSeconds(timePrinting):hh\\:mm\\:ss}";
    graphics.DrawString(timeRemainingText, font, Brushes.Black, new PointF(10, 40));
    graphics.DrawString(timePrintingText, font, Brushes.Black, new PointF(10, 60));

    // Convert the image to a byte array
    using var ms = new MemoryStream();
    image.Save(ms, ImageFormat.Png);
    var imageBytes = ms.ToArray();

    return Results.File(imageBytes, "image/png");
})
.WithName("PrinterStatusImage")
.WithOpenApi();

app.Run();

