using Flurl;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators.Digest;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    {
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
    });

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

ApiEndpoints.apiBaseUrl = Environment.GetEnvironmentVariable("LocalPrusaBaseUrl");
ApiEndpoints.statusPath = "/api/v1/status";
ApiEndpoints.jobPath    = "/api/v1/job";
ApiEndpoints.username   = "maker";
ApiEndpoints.password   = Environment.GetEnvironmentVariable("LocalPrusaApiKey");

app.MapRoutes();

app.Run();

public static class ApiEndpoints
{
    public static string apiBaseUrl = string.Empty;
    public static string statusPath = string.Empty;
    public static string jobPath    = string.Empty;
    public static string username   = string.Empty;
    public static string password   = string.Empty;

    /// <summary>
    /// Create the routes for the API Endpoints.
    /// </summary>
    public static void MapRoutes(this IEndpointRouteBuilder app)
    {
         app.MapGet("/api/status", GetStatusJson)
            .WithName("PrinterStatus")
            .WithOpenApi()
            .Produces(200, typeof(FileContentResult))
            .Produces<FileContentResult>(200, "application/json")
            .ProducesProblem(400);

        app.MapGet("/api/statusImage", GetStatusImage)
           .WithName("PrinterStatusImage")
           .WithOpenApi()
           .Produces<FileContentResult>(200, "image/png")
           .ProducesProblem(400);
    }


    /// <summary>
    /// Gets the current operational status of the printer, including job progress, temperature readings, and other metrics.
    /// </summary>
    /// <remarks>
    /// This endpoint provides a comprehensive overview of the printer's current state, including:
    /// - Job ID and progress percentage
    /// - Estimated time remaining and time spent printing
    /// - Current and target temperatures for the bed and nozzle
    /// - Current state of the printer (e.g., printing, idle, paused)
    /// 
    /// This information is essential for monitoring the printer's activity and performance in real-time.
    /// Note that this is simply a reflection of the printer's stock status API, but is duplicated here to get around
    /// the digest authentication implemented by the printer.
    /// </remarks>
    /// <response code="200">Returns a JSON object containing detailed status information about the printer.</response>
    /// <response code="400">Indicates an error occurred while attempting to retrieve the printer status.</response>
    private static async Task<IResult> GetStatusJson()
    {
        if (string.IsNullOrEmpty(apiBaseUrl))
        {
            return Results.Problem("The printer's base URL was not found in the environment (LocalPrusaBaseUrl), cannot continue.");
        }

        if (string.IsNullOrEmpty(password))
        {
            return Results.Problem("The printer's API key was not found in the environment (LocalPrusaApiKey), cannot continue.");
        }

        var options = new RestClientOptions(apiBaseUrl)
        {
            Authenticator = new DigestAuthenticator(username, password),
            MaxTimeout = 30000,
        };
    
        var client      = new RestClient(options);
        var request     = new RestRequest(statusPath, Method.Get);
        var queryResult = await client.ExecuteAsync(request);
    
        return Results.Text(queryResult.Content, contentType: "application/json");
    }


    #pragma warning disable CA1416 // This method will only work properly in Windows, but I'm fine with that.
    /// <summary>
    /// Retrieves the current status of the printer and generates a visual representation as an image.
    /// </summary>
    /// <remarks>
    /// This endpoint fetches the latest printing status including progress, time remaining, and other details,
    /// and then generates an image that visually represents these metrics. The image includes a progress bar,
    /// time indicators, and a thumbnail related to the current print job if available.
    ///
    /// The progress bar within the image will show the completion percentage, and the text overlay will adjust
    /// based on the progress bar's fill level for optimal readability. The start time of the print job is also
    /// calculated and displayed.
    /// </remarks>
    /// <response code="200">Returns an image file with the visual representation of the current printing status.</response>
    /// <response code="400">Indicates a failure to fetch the printing status from the API or to generate the image.</response>
   private static async Task<IResult> GetStatusImage()
    {
        if (string.IsNullOrEmpty(apiBaseUrl))
        {
            return Results.Problem("The printer's base URL was not found in the environment (LocalPrusaBaseUrl), cannot continue.");
        }

        if (string.IsNullOrEmpty(password))
        {
            return Results.Problem("The printer's API key was not found in the environment (LocalPrusaApiKey), cannot continue.");
        }

        var options = new RestClientOptions(apiBaseUrl)
        {
            Authenticator = new DigestAuthenticator(username, password),
            MaxTimeout = 30000,
        };

        var client = new RestClient(options);
        var request = new RestRequest(jobPath, Method.Get);
        var queryResult = await client.ExecuteAsync(request);

        if (!queryResult.IsSuccessful || string.IsNullOrWhiteSpace(queryResult.Content))
        {
            return Results.Problem("Failed to fetch status from API.");
        }

        // Parse the JSON response
        dynamic? jsonResponse = JsonConvert.DeserializeObject(queryResult.Content);

        if (null == jsonResponse)
        {
            return Results.Problem("Failed to parse result JSON.");
        }

        double progress      = jsonResponse.progress;
        int    timeRemaining = jsonResponse.time_remaining;
        int    timePrinting  = jsonResponse.time_printing;
        string displayName   = jsonResponse.file.display_name;

        // Build the thumbnail URL using flurl
        var thumbnailUrl = Url.Combine(apiBaseUrl, (string)jsonResponse.file.refs.thumbnail);

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
        var thumbnailHeight = 100;
        var aspectRatio = (double)thumbnailImage.Width / thumbnailImage.Height;
        var thumbnailWidth = (int)(thumbnailHeight * aspectRatio);

        // Calculate the print start time
        var currentTime = DateTime.Now;
        var printStartTime = currentTime.AddSeconds(-timePrinting);
        var printStartText = $"Printing Started {(printStartTime.Date == currentTime.Date ? "Today" : "Yesterday")} at {printStartTime:hh:mm tt}";

        // Generate the image with additional height for the title and print start time
        using var image = new Bitmap(500, 140);
        using var graphics = Graphics.FromImage(image);
        graphics.FillRectangle(Brushes.White, 0, 0, image.Width, image.Height); // Background

        // Draw title
        var titleFont = new Font("Arial", 14, FontStyle.Bold);
        graphics.DrawString(displayName, titleFont, Brushes.Black, new PointF((image.Width - graphics.MeasureString(displayName, titleFont).Width) / 2, 10));

        // Adjust layout for thumbnail, progress bar, text elements, and print start time
        var contentYOffset = (int)graphics.MeasureString(displayName, titleFont).Height + 30; // Adjusted for print start time

        // Draw resized thumbnail
        graphics.DrawImage(thumbnailImage, 0, contentYOffset - 30, thumbnailWidth, thumbnailHeight);

        // Determine content X offset based on thumbnail width
        var contentXOffset = thumbnailWidth + 10;

        // Draw print start time aligned with the progress bar
        var timeFont = new Font("Arial", 10);
        graphics.DrawString(printStartText, timeFont, Brushes.Black, new PointF(contentXOffset, contentYOffset - 10)); // Align with progress bar

        // Draw progress bar background (full bar)
        var fullProgressBarWidth = image.Width - contentXOffset - 10; // Full width for 100% progress
        var progressBarBackgroundBrush = Brushes.LightGray; // Light gray for the background of the progress bar
        graphics.FillRectangle(progressBarBackgroundBrush, contentXOffset, contentYOffset + 10, fullProgressBarWidth, 20);

        // Draw progress bar (actual progress)
        var progressBarBrush = Brushes.Green;
        var progressWidth = (int)(progress / 100 * fullProgressBarWidth); // Calculate width based on current progress
        graphics.FillRectangle(progressBarBrush, contentXOffset, contentYOffset + 10, progressWidth, 20);

        // Draw box around progress bar
        var progressBoxPen = new Pen(Color.Black); // Pen for drawing the box
        graphics.DrawRectangle(progressBoxPen, contentXOffset, contentYOffset + 10, fullProgressBarWidth, 20);

        // Prepare to draw percentage text
        var font = new Font("Arial", 10);
        var percentageText = $"{progress}%";
        var textSize = graphics.MeasureString(percentageText, font);
        var textX = contentXOffset + (fullProgressBarWidth / 2) - (textSize.Width / 2); // Center the text in the full progress bar width
        var textY = contentYOffset + 10 + (20 / 2) - (textSize.Height / 2); // Vertically center the text in the progress bar

        // Clip and draw percentage text over the progress bar in white
        var progressClip = new RectangleF(contentXOffset, contentYOffset + 10, progressWidth, 20);
        graphics.SetClip(progressClip);
        graphics.DrawString(percentageText, font, Brushes.White, new PointF(textX, textY));

        // Reset clip and draw percentage text over the remaining bar in black
        graphics.ResetClip();

        var backgroundClip = new RectangleF(contentXOffset + progressWidth, contentYOffset + 10, fullProgressBarWidth - progressWidth, 20);
        graphics.SetClip(backgroundClip);
        graphics.DrawString(percentageText, font, Brushes.Black, new PointF(textX, textY));

        // Reset clip for further drawing
        graphics.ResetClip();

        // Draw time texts
        font = new Font("Arial", 12);
        var timeRemainingText = $"Time Remaining: {TimeSpan.FromSeconds(timeRemaining):hh\\:mm\\:ss}";
        var timePrintingText = $"Time Printing: {TimeSpan.FromSeconds(timePrinting):hh\\:mm\\:ss}";
        graphics.DrawString(timeRemainingText, font, Brushes.Black, new PointF(contentXOffset, contentYOffset + 40));
        graphics.DrawString(timePrintingText, font, Brushes.Black, new PointF(contentXOffset, contentYOffset + 60));

        // Convert the image to a byte array
        using var ms1 = new MemoryStream();
        image.Save(ms1, ImageFormat.Png);
        var imageBytes = ms1.ToArray();

        return Results.File(imageBytes, "image/png");
    }
    #pragma warning restore CA1416 // Validate platform compatibility

}