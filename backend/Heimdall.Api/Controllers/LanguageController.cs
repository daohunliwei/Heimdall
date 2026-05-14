using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
public class LanguageController : ControllerBase
{
    /// <summary>
    /// GET /lang/config — 返回支持的语言列表。
    /// </summary>
    [HttpGet("lang/config")]
    public IActionResult GetLanguageConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config", "lang.json");
        if (!System.IO.File.Exists(configPath))
            return Ok(new { supported_languages = new Dictionary<string, string>(), @default = "en" });

        var json = System.IO.File.ReadAllText(configPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var languages = new Dictionary<string, string>();
        if (root.TryGetProperty("supported_languages", out var sl))
        {
            foreach (var prop in sl.EnumerateObject())
                languages[prop.Name] = prop.Value.GetString() ?? prop.Name;
        }

        var defaultLang = "en";
        if (root.TryGetProperty("default", out var def))
            defaultLang = def.GetString() ?? "en";

        return Ok(new { supported_languages = languages, @default = defaultLang });
    }
}
