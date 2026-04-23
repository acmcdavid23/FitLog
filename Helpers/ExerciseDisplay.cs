namespace FitLog.Helpers
{
    public static class ExerciseDisplay
    {
        public const string PendingInternalName = "__fitlog_pending_exercise__";

        public static bool IsPending(string? name) =>
            string.Equals(name?.Trim(), PendingInternalName, StringComparison.Ordinal);

        public static string Format(string? name) =>
            IsPending(name) ? "— Select exercise —" : (name ?? string.Empty);
    }
}
