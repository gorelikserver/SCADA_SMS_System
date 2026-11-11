using System.ComponentModel.DataAnnotations;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Models
{
    /// <summary>
    /// One rule for every phone number check. The stored format is left to the admin (providers
    /// differ), but a number must hold 7-15 digits, and two numbers with the same digits are the
    /// same phone.
    /// </summary>
    public static class PhoneNumberRules
    {
        public const int MinDigits = 7;
        public const int MaxDigits = 15; // E.164 maximum

        private static readonly char[] AllowedSeparators = { ' ', '-', '(', ')', '.' };

        public static bool IsValid(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;
            var body = phone.Trim();
            if (body.StartsWith('+'))
                body = body[1..];
            if (body.Any(c => !char.IsAsciiDigit(c) && !AllowedSeparators.Contains(c)))
                return false;
            var digits = body.Count(char.IsAsciiDigit);
            return digits is >= MinDigits and <= MaxDigits;
        }

        /// <summary>Digits only, for comparing numbers written with different separators.</summary>
        public static string DigitsOnly(string? phone) =>
            string.Concat((phone ?? string.Empty).Where(char.IsAsciiDigit));

        public static bool SamePhone(string? a, string? b) =>
            DigitsOnly(a).Length > 0 && DigitsOnly(a) == DigitsOnly(b);
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SmsPhoneNumberAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value) =>
            value is null || (value is string s && (s.Length == 0 || PhoneNumberRules.IsValid(s)));

        public override string FormatErrorMessage(string name) => UiLocalizer.Format(
            "Phone number must contain {0}-{1} digits, optionally starting with +, separated only by spaces, dashes, dots or parentheses.",
            PhoneNumberRules.MinDigits, PhoneNumberRules.MaxDigits);
    }
}
