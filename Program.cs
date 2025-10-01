// Program.cs
// dotnet restore && dotnet run
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// 1) Đăng ký CORS (mặc định cho tất cả origin)
//   => Nếu muốn chỉ cho FE 5173, dùng bản named policy ở dưới.
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod()
    );
});

// 2) Các service khác
builder.Services.AddHttpClient("runpod", c =>
{
    c.BaseAddress = new Uri("https://ob7hxqlhpj0sc3-8000.proxy.runpod.net");
    c.Timeout = TimeSpan.FromSeconds(60);
});

// *** LƯU Ý: phải Build từ builder (đừng dùng WebApplication.Create()) ***
var app = builder.Build();

// 3) Kích hoạt CORS (đặt sau Build, trước Map endpoints)
app.UseCors();

// (tuỳ chọn) HTTPS redirection nếu bạn chạy https
// app.UseHttpsRedirection();

// 4) Endpoint gọi LLM và bóc JSON
app.MapPost("/analyze", async (
    [FromServices] IHttpClientFactory httpClientFactory,
    [FromBody] AnalyzeRequest req
) =>
{
    if (string.IsNullOrWhiteSpace(req.Input))
        return Results.BadRequest(new { error = "Input is required." });

    var client = httpClientFactory.CreateClient("runpod");
    var payload = new
    {
        model = "gpt-oss-20b",
        messages = new object[]
        {
            new {
                role = "user",
                content = $"Bạn là chuyên gia phân tích ngôn ngữ tự nhiên tiếng Việt. Nhiệm vụ: hiểu ý nghĩa câu giao việc và trích xuất thông tin thành JSON chuẩn.Hãy phân tích câu sau thành nhiệm vụ cụ thể: Người thực hiện,Nội dung,Thời hạn,Người phối hợp, Độ ưu tiên: {req.Input}"
            }
        },
        temperature = req.Temperature ?? 0.9
    };

    using var httpReq = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
    httpReq.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    using var resp = await client.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead);
    var body = await resp.Content.ReadAsStringAsync();

    if (!resp.IsSuccessStatusCode)
    {
        return Results.Problem(
            title: "Upstream API error",
            detail: body,
            statusCode: (int)resp.StatusCode
        );
    }

    RunpodResponse? runpod;
    try { runpod = JsonSerializer.Deserialize<RunpodResponse>(body); }
    catch (Exception ex) { return Results.Problem("Cannot parse upstream JSON", ex.Message, 500); }

    var content = runpod?.choices?.FirstOrDefault()?.message?.content ?? "";

    string? extractedJson = TryExtractJson(content) ?? TryFindFirstCurlyBlock(content);
    if (string.IsNullOrWhiteSpace(extractedJson))
        return Results.Ok(new { success = false, message = "Không tìm được JSON trong content", rawContent = content });

    try
    {
        using var _ = JsonDocument.Parse(extractedJson);
        return Results.Ok(new { success = true, data = JsonSerializer.Deserialize<object>(extractedJson), rawContent = content });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { success = false, message = "JSON không hợp lệ sau khi trích xuất", error = ex.Message, extracted = extractedJson, rawContent = content });
    }
});

app.Run();

// ==== Helpers / DTOs ====
static string? TryExtractJson(string content)
{
    if (string.IsNullOrWhiteSpace(content)) return null;
    var fenceRegex = new Regex("```(?:json)?\\s*(\\{[\\s\\S]*?\\})\\s*```", RegexOptions.IgnoreCase);
    var m = fenceRegex.Match(content);
    if (m.Success && m.Groups.Count > 1) return CleanQuotes(m.Groups[1].Value);
    var trimmed = content.Trim();
    if (trimmed.StartsWith("{") && trimmed.EndsWith("}")) return CleanQuotes(trimmed);
    return null;
}
static string? TryFindFirstCurlyBlock(string content)
{
    if (string.IsNullOrWhiteSpace(content)) return null;
    var anyJson = new Regex("(\\{[\\s\\S]*\\})");
    var m = anyJson.Match(content);
    return m.Success ? CleanQuotes(m.Groups[1].Value) : null;
}
static string CleanQuotes(string s) =>
    s.Replace('“', '"').Replace('”', '"').Replace('’', '\'').Replace('‘', '\'');

public record AnalyzeRequest(string Input, double? Temperature);
public class RunpodResponse { public string? id { get; set; } public string? model { get; set; } public List<Choice>? choices { get; set; } }
public class Choice { public int index { get; set; } public RPMessage? message { get; set; } public string? finish_reason { get; set; } }
public class RPMessage { public string? role { get; set; } public string? content { get; set; } }
