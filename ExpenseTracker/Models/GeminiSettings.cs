namespace ExpenseTracker.Models
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public List<string> Models { get; set; } = new();

        public IReadOnlyList<string> GetEffectiveModels()
        {
            if (Models.Count > 0)
            {
                return Models;
            }

            return new[]
            {
                "gemini-3.5-flash-lite",
                "gemini-3.7-flash",
                "gemini-3.8-flash"
            };
        }
    }
}