using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services.Providers
{
    /// <summary>
    /// Writes a stored phone number the way a provider expects it. Stored numbers vary
    /// ("050-1234567", "+972 50 1234567", "00972501234567"); providers want one fixed form.
    /// </summary>
    public static class PhoneFormatter
    {
        public static string Format(string? stored, string? format, string? countryCode)
        {
            var phone = stored?.Trim() ?? string.Empty;
            var cc = PhoneNumberRules.DigitsOnly(countryCode);

            switch (format)
            {
                case PhoneFormats.DigitsOnly:
                    return PhoneNumberRules.DigitsOnly(phone);
                case PhoneFormats.E164:
                    return "+" + International(phone, cc);
                case PhoneFormats.International:
                    return International(phone, cc);
                case PhoneFormats.Local:
                    return Local(phone, cc);
                default:
                    return phone;
            }
        }

        // Country code + national number, digits only: "050-1234567" + 972 -> "972501234567".
        private static string International(string phone, string cc)
        {
            var digits = PhoneNumberRules.DigitsOnly(phone);
            if (phone.StartsWith('+'))
                return digits;
            if (digits.StartsWith("00"))
                return digits[2..];
            if (cc.Length > 0 && digits.StartsWith('0'))
                return cc + digits[1..];
            if (cc.Length > 0 && !digits.StartsWith(cc))
                return cc + digits;
            return digits;
        }

        // National form with trunk 0: "+972 50 1234567" + 972 -> "0501234567".
        private static string Local(string phone, string cc)
        {
            var intl = International(phone, cc);
            if (cc.Length > 0 && intl.StartsWith(cc))
                return "0" + intl[cc.Length..];
            return PhoneNumberRules.DigitsOnly(phone);
        }
    }
}
