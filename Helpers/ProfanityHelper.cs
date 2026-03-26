namespace FitLog.Helpers
{
    public static class ProfanityHelper
    {
        private static readonly ProfanityFilter.ProfanityFilter _filter = new ProfanityFilter.ProfanityFilter();

        public static bool ContainsProfanity(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            return _filter.ContainsProfanity(input);
        }

        public static bool IsValidDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.Length < 2 || name.Length > 30) return false;
            if (ContainsProfanity(name)) return false;
            return true;
        }
    }
}