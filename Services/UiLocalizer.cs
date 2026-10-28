using System.Globalization;

namespace SCADASMSSystem.Web.Services;

public interface IUiLocalizer
{
    string this[string english] { get; }
    bool IsHebrew { get; }
}

public sealed class UiLocalizer : IUiLocalizer
{
    private static readonly IReadOnlyDictionary<string, string> Hebrew = new Dictionary<string, string>
    {
        ["SCADA notification gateway"] = "שער התראות SCADA",
        ["Overview"] = "סקירה כללית",
        ["Delivery Alerts"] = "התראות מסירה",
        ["Delivery History"] = "היסטוריית מסירה",
        ["Recipients"] = "נמענים",
        ["Notification Teams"] = "צוותי התראות",
        ["Routing Rules"] = "כללי ניתוב",
        ["System"] = "מערכת",
        ["Configuration"] = "הגדרות",
        ["Integrations & settings"] = "אינטגרציות והגדרות",
        ["Delivery test"] = "בדיקת מסירה",
        ["Schedules & holidays"] = "לוחות זמנים וחגים",
        ["Integration guide"] = "מדריך אינטגרציה",
        ["About"] = "אודות",
        ["Admin"] = "מנהל",
        ["Viewer"] = "צופה",
        ["Change password"] = "שינוי סיסמה",
        ["Accounts"] = "חשבונות",
        ["Sign out"] = "יציאה",
        ["System Status"] = "מצב המערכת",
        ["Cancel"] = "ביטול",
        ["Delete"] = "מחיקה",
        ["Close"] = "סגירה",
        ["English"] = "English",
        ["Hebrew"] = "עברית",
        ["Operator sign in"] = "כניסת מפעיל",
        ["Access alarm routing and delivery operations."] = "גישה לניתוב התראות ולפעולות מסירה.",
        ["User name"] = "שם משתמש",
        ["Password"] = "סיסמה",
        ["Sign in"] = "כניסה",
        ["First-time setup"] = "הגדרה ראשונית",
        ["Create the first admin"] = "יצירת מנהל המערכת הראשון",
        ["No accounts exist yet. This account can manage recipients, notification teams, routing rules, provider configuration, and additional accounts."] = "עדיין לא קיימים חשבונות. חשבון זה יוכל לנהל נמענים, צוותי התראות, כללי ניתוב, הגדרות ספק וחשבונות נוספים.",
        ["Password (at least {0} characters)"] = "סיסמה (לפחות {0} תווים)",
        ["Confirm password"] = "אימות סיסמה",
        ["Create admin and sign in"] = "יצירת מנהל וכניסה",
        ["Current password"] = "סיסמה נוכחית",
        ["New password (at least {0} characters)"] = "סיסמה חדשה (לפחות {0} תווים)",
        ["Confirm new password"] = "אימות סיסמה חדשה",
        ["Password changed."] = "הסיסמה שונתה.",
        ["Access denied"] = "הגישה נדחתה",
        ["Back to overview"] = "חזרה לסקירה הכללית"
    };

    public bool IsHebrew => CultureInfo.CurrentUICulture.Name.StartsWith("he", StringComparison.OrdinalIgnoreCase);

    public string this[string english] => IsHebrew && Hebrew.TryGetValue(english, out var value) ? value : english;
}
