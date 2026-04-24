using PhoneControlServer.Models;
using PhoneControlServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ScreenCaptureService>();
builder.Services.AddSingleton<InputControlService>();
builder.Services.AddSingleton<NetworkInfoService>();
builder.Services.AddSingleton<FileTransferService>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

bool allowControl = true;
int port = 5000;

app.MapGet("/api/status", (NetworkInfoService network) =>
{
    var ip = network.GetLocalIPv4();

    return Results.Ok(new StatusResponse
    {
        Ip = ip,
        Port = port,
        AllowControl = allowControl,
        Message = "Server đang hoạt động",
        FileTransferUrl = $"http://{ip}:{port}"
    });
});

app.MapGet("/api/screen", (ScreenCaptureService screen) =>
{
    byte[] imageBytes = screen.CaptureJpeg(900, 45);
    return Results.File(imageBytes, "image/jpeg");
});

app.MapGet("/api/files", (FileTransferService files) =>
{
    return Results.Ok(files.GetFiles());
});

app.MapGet("/api/files/{id}", (string id, FileTransferService files) =>
{
    var stream = files.OpenRead(id);
    if (stream is null)
    {
        return Results.NotFound(new { message = "Không tìm thấy file" });
    }

    var downloadName = files.GetDownloadName(id) ?? "download.bin";
    return Results.File(
        stream,
        contentType: "application/octet-stream",
        fileDownloadName: downloadName,
        enableRangeProcessing: true);
});

app.MapPost("/api/files/upload", async (HttpRequest request, FileTransferService files, CancellationToken cancellationToken) =>
{
    var fileName = request.Headers["X-File-Name"].ToString();
    if (string.IsNullOrWhiteSpace(fileName))
    {
        return Results.BadRequest(new { message = "Thiếu tên file" });
    }

    if (request.ContentLength is null or <= 0)
    {
        return Results.BadRequest(new { message = "File rỗng hoặc không xác định kích thước" });
    }

    var savedFile = await files.SaveFileAsync(request.Body, fileName, cancellationToken);
    return Results.Ok(savedFile);
});

app.MapPost("/api/mouse", (MouseRequest request, InputControlService input) =>
{
    if (!allowControl)
    {
        return Results.BadRequest(new { message = "Điều khiển đang bị tắt" });
    }

    input.HandleMouse(request);
    return Results.Ok(new { message = "OK" });
});

app.MapPost("/api/key", (KeyRequest request, InputControlService input) =>
{
    if (!allowControl)
    {
        return Results.BadRequest(new { message = "Điều khiển đang bị tắt" });
    }

    input.HandleKey(request);
    return Results.Ok(new { message = "OK" });
});

app.MapPost("/api/control/toggle", () =>
{
    allowControl = !allowControl;
    return Results.Ok(new { allowControl });
});

app.Urls.Add($"http://0.0.0.0:{port}");
app.Run();
