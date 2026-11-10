using System.ComponentModel.DataAnnotations;

namespace SCADASMSSystem.Web.Services;

public sealed class LocalizedRequiredAttribute : RequiredAttribute
{
    public override string FormatErrorMessage(string name) =>
        UiLocalizer.Format("The {0} field is required.", UiLocalizer.Localize(name));
}

public sealed class LocalizedStringLengthAttribute(int maximumLength) : StringLengthAttribute(maximumLength)
{
    public override string FormatErrorMessage(string name) => MinimumLength > 0
        ? UiLocalizer.Format(
            "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}.",
            UiLocalizer.Localize(name), MaximumLength, MinimumLength)
        : UiLocalizer.Format(
            "The field {0} must be a string with a maximum length of {1}.",
            UiLocalizer.Localize(name), MaximumLength);
}

/// <summary>
/// Resource accessors for sealed DataAnnotations attributes (<see cref="PhoneAttribute"/>, <see cref="EmailAddressAttribute"/>)
/// that cannot be subclassed. Each property is read per validation, so the message follows the
/// current request's UI culture.
/// </summary>
public static class LocalizedValidationMessages
{
    public static string Phone => UiLocalizer.Localize("The {0} field is not a valid phone number.");

    public static string Email => UiLocalizer.Localize("The {0} field is not a valid e-mail address.");
}
