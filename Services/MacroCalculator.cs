using FitLog.Models;

namespace FitLog.Services
{
    public static class MacroCalculator
    {
        public static MacroCalculationResult? TryCalculate(MacroCalculationRequest input, out string? error)
        {
            error = null;
            if (input.Age < 13 || input.Age > 120)
            {
                error = "Enter a valid age (13–120).";
                return null;
            }

            if (input.HeightInches < 40 || input.HeightInches > 96)
            {
                error = "Enter a realistic height in inches (about 40–96).";
                return null;
            }

            var weightKg = ToKg(input.CurrentWeight, input.WeightUnit);
            if (weightKg < 30 || weightKg > 300)
            {
                error = "Enter a realistic current weight.";
                return null;
            }

            var heightCm = (double)input.HeightInches * 2.54;
            var w = (double)weightKg;
            var a = input.Age;
            var isFemale = string.Equals(input.Gender?.Trim(), "Female", StringComparison.OrdinalIgnoreCase);
            var bmr = isFemale
                ? 10 * w + 6.25 * heightCm - 5 * a - 161
                : 10 * w + 6.25 * heightCm - 5 * a + 5;

            var factor = ActivityFactor(input.ActivityLevel);
            var tdee = bmr * factor;

            var calorieTarget = BodyGoalCalories(tdee, input.BodyGoal);
            var weightLbs = string.Equals(input.WeightUnit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase)
                ? (double)input.CurrentWeight * 2.2046226218
                : (double)input.CurrentWeight;

            var proteinPerLb = ProteinPerLb(input.BodyGoal, input.FitnessGoal);
            var proteinG = Math.Round(weightLbs * proteinPerLb);
            proteinG = Math.Clamp(proteinG, 50, 400);

            var fatPct = FatPercent(input.BodyGoal);
            var fatG = Math.Round((calorieTarget * fatPct) / 9.0);
            fatG = Math.Clamp(fatG, 30, 200);

            var carbCal = calorieTarget - proteinG * 4 - fatG * 9;
            var carbG = Math.Round(carbCal / 4.0);
            if (carbG < 50)
            {
                fatG = Math.Max(30, fatG - 15);
                carbCal = calorieTarget - proteinG * 4 - fatG * 9;
                carbG = Math.Round(carbCal / 4.0);
            }

            carbG = Math.Clamp(carbG, 50, 800);

            var caloriesRounded = (int)Math.Round(Math.Max(1000, Math.Min(10000, calorieTarget)));

            return new MacroCalculationResult
            {
                Calories = caloriesRounded,
                Protein = (int)proteinG,
                Carbs = (int)carbG,
                Fat = (int)fatG,
                Bmr = (int)Math.Round(bmr),
                Tdee = (int)Math.Round(tdee)
            };
        }

        private static double ToKg(decimal weight, string? unit)
        {
            if (string.Equals(unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase))
                return (double)weight;
            return (double)weight * 0.45359237;
        }

        private static double ActivityFactor(string? level)
        {
            return level?.Trim() switch
            {
                "Sedentary" => 1.2,
                "Light" => 1.375,
                "Moderate" => 1.55,
                "Active" => 1.725,
                "VeryActive" => 1.9,
                _ => 1.55
            };
        }

        private static double BodyGoalCalories(double tdee, string? bodyGoal)
        {
            return bodyGoal?.Trim() switch
            {
                "Bulk" => tdee * 1.08,
                "Cut" => tdee * 0.82,
                _ => tdee
            };
        }

        private static double ProteinPerLb(string? bodyGoal, string? fitnessGoal)
        {
            var baseFromFitness = fitnessGoal?.Trim() switch
            {
                "Strength" => 1.0,
                "Hypertrophy" => 0.95,
                "Weight Loss" => 1.0,
                "Conditioning" => 0.85,
                _ => 0.85
            };

            var floor = bodyGoal?.Trim() switch
            {
                "Cut" => 0.95,
                "Bulk" => 0.8,
                _ => 0.75
            };

            return Math.Max(floor, baseFromFitness);
        }

        private static double FatPercent(string? bodyGoal) =>
            bodyGoal?.Trim() switch
            {
                "Cut" => 0.28,
                "Bulk" => 0.30,
                _ => 0.28
            };
    }
}
