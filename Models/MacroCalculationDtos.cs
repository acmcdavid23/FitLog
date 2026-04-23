namespace FitLog.Models
{
    public class MacroCalculationRequest
    {
        public int Age { get; set; }
        public string? Gender { get; set; }
        public decimal HeightInches { get; set; }
        public decimal CurrentWeight { get; set; }
        public string? WeightUnit { get; set; }
        public string? ActivityLevel { get; set; }
        public string? BodyGoal { get; set; }
        public string? FitnessGoal { get; set; }
    }

    public class MacroCalculationResult
    {
        public int Calories { get; set; }
        public int Protein { get; set; }
        public int Carbs { get; set; }
        public int Fat { get; set; }
        public int Bmr { get; set; }
        public int Tdee { get; set; }
    }

    public class MacroCalculationJsonResponse
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public MacroCalculationResult? Data { get; set; }
    }
}
