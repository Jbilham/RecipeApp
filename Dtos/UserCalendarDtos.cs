namespace RecipeApp.Dtos
{
    public class SetTrainingPeaksUrlDto
    {
        public Guid? UserId { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    public class ImportTrainingPeaksDto
    {
        public Guid? UserId { get; set; }
        public string? UrlOverride { get; set; }
    }
}
