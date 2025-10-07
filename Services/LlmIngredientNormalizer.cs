using OpenAI.Chat;
using System.Text.Json;

namespace RecipeApp.Services
{
    public class LlmIngredientNormalizer
    {
        private readonly OpenAIClient _client;
        public LlmIngredientNormalizer(OpenAIClient client)
        {
            _client = client;
        }

        public async Task<Dictionary<string, string>> NormalizeAsync(IEnumerable<string> ingredientNames)
        {
            var inputList = ingredientNames.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (!inputList.Any()) return new();

            var prompt = @$"
You are a food and grocery data normaliser.
Given a list of ingredient names, return a JSON dictionary mapping each original name to its cleaned, normalised version.

- Use singular forms
- Remove brand names and descriptive noise
- Combine variants (e.g. 'Tomatoes' and 'Cherry Tomatoes' → 'Tomato')
- Simplify similar protein items (e.g. 'Protein Shake', 'Whey Protein Drink' → 'Protein Powder')
- Keep plain words (no emojis, punctuation)
- Return JSON only

Input:
{JsonSerializer.Serialize(inputList, new JsonSerializerOptions { WriteIndented = true })}
";

            var chat = new ChatRequest
            {
                Model = "gpt-4o-mini",
                Messages = [new ChatMessage("system", prompt)]
            };

            var result = await _client.Chat.GetAsync(chat);
            var text = result.Value.Choices[0].Message.Content[0].Text;

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(text ?? "{}")
                       ?? new Dictionary<string, string>();
            }
            catch
            {
                return inputList.ToDictionary(x => x, x => x);
            }
        }
    }
}
