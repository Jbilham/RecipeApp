using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

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
                var oa = new OpenAIClient(_apiKey);
                var chatClient = oa.GetChatClient("gpt-4o-mini");
                var resp = await chatClient.CompleteChatAsync(new ChatMessage[]
                {
                    ChatMessage.CreateSystemMessage("You are a health-check probe. Reply with a short confirmation string."),
                    ChatMessage.CreateUserMessage("Return the text: LLM_OK")
                });

                var text = resp.Value.Content[0].Text ?? string.Empty;
                return Ok(new { ok = true, text });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ok = false, error = ex.Message });
            }
        }
    }
}
