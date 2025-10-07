namespace RecipeApp.Dtos
{
    public class UploadRecipeImageDto
    {
        /// <summary>
        /// One or more recipe image files.
        /// </summary>
        public List<IFormFile> Files { get; set; } = new();
    }
}