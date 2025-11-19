using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Dtos;
using RecipeApp.Models;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RecipeApp.Services;

/// <summary>
/// Builds a normalised, deduplicated, alphabetised shopping list
/// from recipe data and free-text meal extras.
/// </summary>
public class ShoppingListBuilder
{
    private readonly AppDb _db;
    private readonly LlmIngredientNormalizer _normalizer;

    private static readonly string[] CategoryOrder =
    {
        "Produce",
        "Protein",
        "Dairy & Eggs",
        "Bakery & Grains",
        "Pantry",
        "Snacks & Supplements",
        "Beverages",
        "Condiments & Sauces",
        "Other"
    };

    public ShoppingListBuilder(AppDb db, LlmIngredientNormalizer normalizer)
    {
        _db = db;
        _normalizer = normalizer;
    }

    /// <summary>
    /// Aggregates all recipe ingredients and free-text items into a single list,
    /// applying heuristic merging plus LLM-backed ingredient normalisation.
    /// </summary>
    public async Task<ShoppingListResponse> BuildAsync(
        IEnumerable<Guid> recipeIds,
        IEnumerable<string>? extraItems = null)
    {
        Console.WriteLine("🔹 Building shopping list…");

        // --- 1️⃣ Gather recipe ingredients ---
        var ids = recipeIds?.Distinct().ToList() ?? new();
        var rows = await _db.RecipeIngredients
            .Include(ri => ri.Ingredient)
            .Include(ri => ri.Unit)
            .Where(ri => ids.Contains(ri.RecipeId))
            .AsNoTracking()
            .ToListAsync();

        static string NormalizeKey(string input) =>
            input.ToLowerInvariant()
                 .Replace("&", "and")
                 .Replace("-", " ")
                 .Replace("x ", "")
                 .Replace("+", " ")
                 .Replace("/", " ")
                 .Replace("  ", " ")
                 .Trim();

        static string Capitalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            return textInfo.ToTitleCase(value.Trim().ToLowerInvariant());
        }

        static string DetermineCategory(string ingredient)
        {
            if (string.IsNullOrWhiteSpace(ingredient)) return "Other";

            var name = ingredient.ToLowerInvariant();

            bool Contains(params string[] keywords) => keywords.Any(k => name.Contains(k));
            bool ContainsAll(params string[] keywords) => keywords.All(k => name.Contains(k));

            if (Contains("water", "juice", "smoothie"))
                return "Beverages";

            if (ContainsAll("protein", "shake") || Contains("protein yoghurt") || Contains("protein yogurt"))
                return "Snacks & Supplements";

            if (Contains("protein bar") || Contains("snack", "bar"))
                return "Snacks & Supplements";

            if (Contains("fruit") || Contains("apple") || Contains("banana") || Contains("pear") || Contains("orange") ||
                Contains("berry") || Contains("avocado") || Contains("grape") || Contains("melon") || Contains("lemon") ||
                Contains("lime") || Contains("lettuce") || Contains("spinach") || Contains("salad") || Contains("pepper") ||
                Contains("tomato") || Contains("onion") || Contains("garlic") || Contains("broccoli") || Contains("vegetable") ||
                Contains("coriander") || Contains("herb") || Contains("parsley") || Contains("cucumber"))
                return "Produce";

            if (Contains("chicken") || Contains("beef") || Contains("steak") || Contains("pork") || Contains("tofu") ||
                Contains("halloumi") || Contains("salmon") || Contains("tuna") || Contains("pancetta") || Contains("fish") ||
                Contains("turkey") || Contains("protein") && !Contains("shake"))
                return "Protein";

            if (Contains("egg") || Contains("milk") || Contains("cheese") || Contains("yoghurt") || Contains("yogurt") ||
                Contains("butter") || Contains("cream"))
                return "Dairy & Eggs";

            if (Contains("bread") || Contains("toast") || Contains("wrap") || Contains("tortilla") || Contains("pasta") ||
                Contains("rice") || Contains("quinoa") || Contains("grain") || Contains("bagel") || Contains("oat") ||
                Contains("noodle") || Contains("couscous") || Contains("brioche"))
                return "Bakery & Grains";

            if (Contains("vinegar") || Contains("sauce") || Contains("pesto") || Contains("paste") || Contains("dressing") ||
                Contains("stock") || Contains("broth"))
                return "Condiments & Sauces";

            if (Contains("spice") || Contains("cumin") || Contains("cinnamon") || Contains("turmeric") || Contains("pepper") ||
                Contains("oregano") || Contains("coriander") || Contains("bay") || Contains("paprika") || Contains("seasoning") ||
                Contains("powder") || Contains("salt") || Contains("herb") || Contains("seed") || Contains("nuts") ||
                Contains("oil") || Contains("vinegar") || Contains("stock") || Contains("beans") || Contains("lentil") ||
                Contains("canned") || Contains("tin"))
                return "Pantry";

            return "Other";
        }

        static int CategoryRank(string category)
        {
            var index = Array.IndexOf(CategoryOrder, category);
            return index >= 0 ? index : CategoryOrder.Length;
        }

        static string? NormalizeUnit(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return null;
            return unit.ToLowerInvariant() switch
            {
                "slices" => "slice",
                "slice" => "slice",
                "pieces" => "pcs",
                "piece" => "pcs",
                "pc" => "pcs",
                "portion" => "portion",
                "portions" => "portion",
                "tablespoon" => "tbsp",
                "tablespoons" => "tbsp",
                "tbsp" => "tbsp",
                "teaspoon" => "tsp",
                "teaspoons" => "tsp",
                "tsp" => "tsp",
                "grams" => "g",
                "gram" => "g",
                "g" => "g",
                "kilogram" => "kg",
                "kilograms" => "kg",
                "kg" => "kg",
                "millilitre" => "ml",
                "millilitres" => "ml",
                "ml" => "ml",
                "litre" => "l",
                "litres" => "l",
                "l" => "l",
                _ => unit.ToLowerInvariant()
            };
        }

        static IEnumerable<string> SplitExtraComponents(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) yield break;

            var normalised = Regex.Replace(input, @"\b(with|and|plus|served with|topped with|paired with)\b", ",", RegexOptions.IgnoreCase);
            normalised = normalised.Replace("&", ",").Replace("/", ",").Replace("+", ",");

            foreach (var part in normalised.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    yield return trimmed;
            }
        }

        static IEnumerable<(string name, decimal? amount, string? unit)> MapExtraToItems(string rawName, decimal? amount, string? unit)
        {
            var name = rawName?.Trim() ?? string.Empty;
            var lower = name.ToLowerInvariant();
            var results = new List<(string, decimal?, string?)>();
            var normalizedUnit = NormalizeUnit(unit);

            bool ContainsAll(params string[] keywords) => keywords.All(k => lower.Contains(k));
            bool ContainsAny(params string[] keywords) => keywords.Any(k => lower.Contains(k));

            void Add(string itemName, decimal? qty = null, string? itemUnit = null)
            {
                if (string.IsNullOrWhiteSpace(itemName)) return;
                results.Add((itemName, qty, itemUnit));
            }

            if (ContainsAll("portion", "fruit"))
            {
                Add("Fruit", amount ?? 1, "pcs");
                return results;
            }

            if (lower.Contains("protein bar"))
            {
                Add("Protein Bar", 1, "pcs");
                return results;
            }

            if (ContainsAny("protein yoghurt", "protein yogurt"))
            {
                Add("Protein Yoghurt", 1, "tub");
                return results;
            }

            if (ContainsAny("whey protein", "protein shake"))
            {
                Add("Protein Shake", 1, "portion");
                return results;
            }

            if (lower.Contains("banana"))
            {
                var qty = amount;
                if (!qty.HasValue && lower.Contains("half")) qty = 0.5m;
                Add("Fruit", qty ?? 1, "pcs");
                return results;
            }

            if (lower.Contains("apple"))
            {
                var qty = amount;
                if (!qty.HasValue && lower.Contains("half")) qty = 0.5m;
                Add("Fruit", qty ?? 1, "pcs");
                return results;
            }

            if (lower.Contains("pear"))
            {
                var qty = amount;
                if (!qty.HasValue && lower.Contains("half")) qty = 0.5m;
                Add("Fruit", qty ?? 1, "pcs");
                return results;
            }

            if (lower.Contains("orange"))
            {
                var qty = amount;
                if (!qty.HasValue && lower.Contains("half")) qty = 0.5m;
                Add("Fruit", qty ?? 1, "pcs");
                return results;
            }

            if (lower.Contains("avocado"))
            {
                var qty = amount;
                if (!qty.HasValue && lower.Contains("half")) qty = 0.5m;
                Add("Avocado", qty ?? 1, normalizedUnit ?? "pcs");
                return results;
            }

            if (lower.Contains("chicken breast"))
            {
                Add("Chicken Breast", amount ?? 1, normalizedUnit ?? "pcs");
                return results;
            }

            if (lower.Contains("garlic clove") || lower.Contains("clove garlic"))
            {
                Add("Garlic Clove", amount ?? 1, normalizedUnit ?? "pcs");
                return results;
            }

            if (lower.Contains("egg"))
            {
                Add("Eggs", amount ?? 1, normalizedUnit ?? "pcs");
                return results;
            }

            if (lower.Contains("toast") || lower.Contains("bread"))
            {
                var slices = amount;
                if (!slices.HasValue && lower.Contains("slice")) slices = 1;
                Add("Wholemeal Bread", slices, "slice");
                if (lower.Contains("egg")) Add("Eggs", amount ?? 1, "pcs");
                if (lower.Contains("avocado")) Add("Avocado", 0.5m, "pcs");
                if (lower.Contains("butter")) Add("Butter", null, null);
                return results;
            }

            if (ContainsAny("omelette", "omelet"))
            {
                Add("Eggs", amount ?? 3, "pcs");
                if (ContainsAny("veg", "veggie", "vegetable"))
                    Add("Mixed Vegetables", null, null);
                if (lower.Contains("spinach"))
                    Add("Spinach", null, null);
                return results;
            }

            if (lower.Contains("salad"))
            {
                if (lower.Contains("chicken"))
                    Add("Chicken Breast", amount ?? 1, "pcs");
                if (lower.Contains("tuna"))
                    Add("Tuna", amount ?? 1, "tin");
                if (lower.Contains("halloumi"))
                    Add("Halloumi", amount ?? 1, null);
                if (lower.Contains("egg"))
                    Add("Eggs", amount ?? 1, "pcs");
                Add("Mixed Salad", null, null);
                return results;
            }

            if (lower.Contains("wrap"))
            {
                Add("Tortilla Wrap", amount ?? 1, null);
                if (lower.Contains("chicken"))
                    Add("Chicken Breast", amount ?? 1, "pcs");
                return results;
            }

            if (lower.Contains("quinoa"))
            {
                Add("Quinoa", amount, normalizedUnit);
                if (lower.Contains("lemon"))
                    Add("Lemon", 1, "pcs");
                if (lower.Contains("chicken"))
                    Add("Chicken Breast", amount ?? 1, "pcs");
                return results;
            }

            if (lower.Contains("pasta"))
            {
                Add("Pasta", amount, normalizedUnit ?? "g");
                if (lower.Contains("broccoli"))
                    Add("Broccoli", null, null);
                if (lower.Contains("pancetta"))
                    Add("Pancetta", null, null);
                return results;
            }

            if (lower.Contains("stew"))
            {
                Add("Stewing Steak", null, null);
                Add("Mixed Vegetables", null, null);
                Add("Beans", null, null);
                return results;
            }

            if (ContainsAny("protein", "whey") && lower.Contains("shake"))
            {
                Add("Protein Shake", amount ?? 1, "portion");
                return results;
            }

            if (lower.Contains("fruit"))
            {
                Add("Fruit", amount ?? 1, normalizedUnit);
                return results;
            }

            if (!string.IsNullOrWhiteSpace(name))
                Add(name, amount, normalizedUnit);

            return results;
        }

        static ShoppingListItemDto Clone(ShoppingListItemDto source) =>
            new()
            {
                Ingredient = source.Ingredient,
                Amount = source.Amount,
                Unit = source.Unit,
                SourceRecipes = source.SourceRecipes != null
                    ? new List<Guid>(source.SourceRecipes)
                    : new List<Guid>()
            };

        static void MergeItems(ShoppingListItemDto target, ShoppingListItemDto incoming)
        {
            if (!target.Amount.HasValue && incoming.Amount.HasValue)
            {
                target.Amount = incoming.Amount;
                if (!string.IsNullOrWhiteSpace(incoming.Unit))
                    target.Unit = incoming.Unit;
            }
            else if (target.Amount.HasValue && incoming.Amount.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(target.Unit) &&
                    !string.IsNullOrWhiteSpace(incoming.Unit) &&
                    string.Equals(target.Unit, incoming.Unit, StringComparison.OrdinalIgnoreCase))
                {
                    target.Amount += incoming.Amount;
                }
                else if (string.IsNullOrWhiteSpace(target.Unit) && !string.IsNullOrWhiteSpace(incoming.Unit))
                {
                    target.Unit = incoming.Unit;
                }
            }
            else if (!string.IsNullOrWhiteSpace(incoming.Unit) && string.IsNullOrWhiteSpace(target.Unit))
            {
                target.Unit = incoming.Unit;
            }

            if (incoming.SourceRecipes != null)
            {
                foreach (var id in incoming.SourceRecipes)
                {
                    if (!target.SourceRecipes.Contains(id))
                        target.SourceRecipes.Add(id);
                }
            }
        }

        var grouped = new Dictionary<string, ShoppingListItemDto>(StringComparer.OrdinalIgnoreCase);

        void AddOrUpdate(string key, ShoppingListItemDto value)
        {
            if (!grouped.TryGetValue(key, out var existing))
            {
                grouped[key] = value;
            }
            else
            {
                MergeItems(existing, value);
            }
        }

        void AddItem(string name, decimal? qty, string? unit, IEnumerable<Guid>? sources = null, bool fromExtra = false)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            var dto = new ShoppingListItemDto
            {
                Ingredient = Capitalize(name),
                Amount = qty,
                Unit = NormalizeUnit(unit),
                SourceRecipes = sources != null ? new List<Guid>(sources) : new List<Guid>()
            };

            if (fromExtra)
            {
                if (!dto.Amount.HasValue || dto.Amount.Value <= 0)
                {
                    dto.Amount = 1;
                }

                if (!string.IsNullOrWhiteSpace(dto.Unit))
                {
                    var normalized = dto.Unit;
                    if (normalized == "g" || normalized == "kg" || normalized == "ml" || normalized == "l")
                    {
                        dto.Unit = null;
                    }
                }
            }

            var key = NormalizeKey(dto.Ingredient);
            AddOrUpdate(key, dto);
        }

        foreach (var ri in rows)
        {
            AddItem(ri.Ingredient.Name, ri.Amount, ri.Unit?.Code, new[] { ri.RecipeId }, false);
        }

        // --- 2️⃣ Add free-text items (e.g., LLM meal parser output) ---
        if (extraItems != null)
        {
            foreach (var extra in extraItems)
            {
                if (string.IsNullOrWhiteSpace(extra)) continue;

                var segments = SplitExtraComponents(extra).ToList();
                if (segments.Count == 0) segments.Add(extra);

                var produced = false;

                foreach (var segment in segments)
                {
                    var parsed = LlmMealPlanParser.ParseIngredientText(segment);
                    var baseName = string.IsNullOrWhiteSpace(parsed.name) ? segment : parsed.name;
                    decimal? amount = parsed.qty.HasValue
                        ? Math.Round((decimal)parsed.qty.Value, 2)
                        : null;
                    var mappedItems = MapExtraToItems(baseName, amount, parsed.unit);

                    var any = false;
                    foreach (var (itemName, qty, itemUnit) in mappedItems)
                    {
                        AddItem(itemName, qty, itemUnit, null, true);
                        any = true;
                    }

                    produced |= any;
                }

                if (!produced)
                {
                    AddItem(extra, null, null, null, true);
                }
            }
        }

        var initialItems = grouped.Values.ToList();

        // --- 3️⃣ Normalise ingredient names using the LLM-backed normalizer ---
        var nameMappings = await _normalizer.NormalizeAsync(initialItems.Select(i => i.Ingredient));

        var merged = new Dictionary<string, ShoppingListItemDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in initialItems)
        {
            var canonical = nameMappings.TryGetValue(item.Ingredient, out var mapped)
                ? mapped
                : item.Ingredient;

            canonical = Capitalize(canonical);

            var clone = Clone(item);
            clone.Ingredient = canonical;

            AddOrMerge(merged, clone);
        }

        if (merged.Count == 0)
        {
            Console.WriteLine("⚠️ Shopping list generation produced no items.");
            return new ShoppingListResponse();
        }

        var finalList = merged.Values
            .Select(item =>
            {
                item.Ingredient = Capitalize(item.Ingredient);
                item.Category = DetermineCategory(item.Ingredient);
                return item;
            })
            .OrderBy(i => CategoryRank(i.Category))
            .ThenBy(i => i.Ingredient)
            .ToList();

        Console.WriteLine($"✅ Shopping list built ({finalList.Count} items)");
        return new ShoppingListResponse { Items = finalList };

        void AddOrMerge(Dictionary<string, ShoppingListItemDto> destination, ShoppingListItemDto value)
        {
            if (!destination.TryGetValue(value.Ingredient, out var existing))
            {
                destination[value.Ingredient] = value;
            }
            else
            {
                MergeItems(existing, value);
            }
        }
    }
}
