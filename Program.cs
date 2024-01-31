using System.Drawing;
using System.Drawing.Imaging;
using System.Dynamic;
using System.Threading;
using Flurl;
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
    var displayName = jsonResponse.file.display_name;

    // Build the thumbnail URL using flurl
    var thumbnailUrl = Url.Combine(apiBaseUrl, jsonResponse.file.refs.thumbnail);

    // Fetch the thumbnail image
    var thumbnailRequest = new RestRequest(thumbnailUrl, Method.Get);
    var thumbnailResponse = await client.ExecuteAsync(thumbnailRequest);
    if (!thumbnailResponse.IsSuccessful)
    {
        return Results.Problem("Failed to fetch thumbnail.");
    }

    Image thumbnailImage;
    using (var ms = new MemoryStream(thumbnailResponse.RawBytes))
    {
        thumbnailImage = Image.FromStream(ms);
    }

    // Resize thumbnail if necessary
    int thumbnailHeight = 100;
    double aspectRatio = (double)thumbnailImage.Width / thumbnailImage.Height;
    int thumbnailWidth = (int)(thumbnailHeight * aspectRatio);

    // Generate the image
    using var image = new Bitmap(500, 100); // Increased width to accommodate thumbnail
    using var graphics = Graphics.FromImage(image);
    graphics.FillRectangle(Brushes.White, 0, 0, image.Width, image.Height); // Background

    // Draw resized thumbnail
    graphics.DrawImage(thumbnailImage, 0, 0, thumbnailWidth, thumbnailHeight);

    // Adjust layout for progress bar and text elements
    int contentXOffset = thumbnailWidth + 10; // Start drawing content to the right of the thumbnail

    // Draw progress bar
    var progressBarBrush = Brushes.Green;
    var progressWidth = (int)(progress / 100 * (image.Width - contentXOffset - 10)); // Calculate width based on progress
    graphics.FillRectangle(progressBarBrush, contentXOffset, 10, progressWidth, 20);

    // Draw percentage text
    var font = new Font("Arial", 10);
    var percentageText = $"{progress}%";
    var textSize = graphics.MeasureString(percentageText, font);
    var textX = contentXOffset + (progressWidth / 2) - (textSize.Width / 2); // Center the text in the progress bar
    var textY = 10 + (20 / 2) - (textSize.Height / 2); // Vertically center the text in the progress bar
    graphics.DrawString(percentageText, font, Brushes.Black, new PointF(textX, textY));

    // Draw time texts
    font = new Font("Arial", 12);
    var timeRemainingText = $"Time Remaining: {TimeSpan.FromSeconds(timeRemaining):hh\\:mm\\:ss}";
    var timePrintingText = $"Time Printing: {TimeSpan.FromSeconds(timePrinting):hh\\:mm\\:ss}";
    graphics.DrawString(timeRemainingText, font, Brushes.Black, new PointF(contentXOffset, 40));
    graphics.DrawString(timePrintingText, font, Brushes.Black, new PointF(contentXOffset, 60));

    // Convert the image to a byte array
    using var ms1 = new MemoryStream();
    image.Save(ms1, ImageFormat.Png);
    var imageBytes = ms1.ToArray();

    return Results.File(imageBytes, "image/png");
})
.WithName("PrinterStatusImage")
.WithOpenApi();

app.Run();

