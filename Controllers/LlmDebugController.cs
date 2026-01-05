using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LlmDebugController : ControllerBase
    {
        private readonly string _apiKey;
        private readonly IHttpClientFactory _httpClientFactory;

        public LlmDebugController(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing");
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("ping")]
        public async Task<IActionResult> Ping()
        {
            try
            {
                var model = "gpt-4o-mini";
                var messages = new object[]
                {
                    new { role = "system", content = "You are a health-check probe. Reply with a short confirmation string." },
                    new { role = "user", content = "Return the text: LLM_OK" }
                };

                var client = _httpClientFactory.CreateClient();
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                req.Content = JsonContent.Create(new { model, messages });

                var resp = await client.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    return StatusCode((int)resp.StatusCode, new { ok = false, error = body, request = new { model, messages } });
                }

                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                return Ok(new
                {
                    ok = true,
                    text,
                    request = new
                    {
                        model,
                        messages
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ok = false, error = ex.Message });
            }
        }
    }
}
