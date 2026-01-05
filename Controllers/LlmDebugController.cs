using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.ClientModel;

namespace RecipeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LlmDebugController : ControllerBase
    {
        private readonly string _apiKey;

        public LlmDebugController(IConfiguration config)
        {
            _apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing");
        }

        [HttpGet("ping")]
        public async Task<IActionResult> Ping()
        {
            try
            {
                var model = "gpt-4o-mini";
                var chatClient = new ChatClient(model, new ApiKeyCredential(_apiKey));

                var messages = new ChatMessage[]
                {
                    ChatMessage.CreateSystemMessage("You are a health-check probe. Reply with a short confirmation string."),
                    ChatMessage.CreateUserMessage("Return the text: LLM_OK")
                };

                var resp = await chatClient.CompleteChatAsync(messages);

                var text = resp.Value.Content[0].Text ?? string.Empty;
                return Ok(new
                {
                    ok = true,
                    text,
                    request = new
                    {
                        model,
                        messages = messages.Select(m => new { type = m.GetType().Name, content = m.Content[0].Text })
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
