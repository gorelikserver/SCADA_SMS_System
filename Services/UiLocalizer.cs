using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

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
        ["About AlarmBridge"] = "אודות AlarmBridge",
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
        ["Back to overview"] = "חזרה לסקירה הכללית",
        ["Operations overview"] = "סקירת תפעול",
        ["Test SMS"] = "בדיקת SMS",
        ["Integration Guide"] = "מדריך אינטגרציה",
        ["SMS Provider"] = "ספק SMS",
        ["Settings History"] = "היסטוריית הגדרות",
        ["Coverage Calendar"] = "לוח כיסוי",
        ["System Settings"] = "הגדרות מערכת",
        ["Edit Recipient"] = "עריכת נמען",
        ["Add Recipient"] = "הוספת נמען",
        ["Edit Notification Team"] = "עריכת צוות התראות",
        ["Create Notification Team"] = "יצירת צוות התראות",
        ["Error"] = "שגיאה",
        ["Primary navigation"] = "ניווט ראשי",
        ["Toggle navigation"] = "פתיחה או סגירה של הניווט",
        ["Language"] = "שפה",
        ["Alarm {0} acknowledged."] = "התראה {0} אושרה.",
        ["Group '{0}' has been created successfully!"] = "הצוות '{0}' נוצר בהצלחה!",
        ["Group '{0}' has been updated successfully!"] = "הצוות '{0}' עודכן בהצלחה!",
        ["Added {0} to the group successfully!"] = "{0} נוסף לצוות בהצלחה!",
        ["Removed {0} from the group successfully!"] = "{0} הוסר מהצוות בהצלחה!",
        ["Failed to remove user from group."] = "הסרת המשתמש מהצוות נכשלה.",
        ["An error occurred while loading alarm data."] = "אירעה שגיאה בטעינת נתוני ההתראות.",
        ["Successfully added group '{0}' to the alarm."] = "הצוות '{0}' נוסף להתראה בהצלחה.",
        ["Failed to add group to alarm. It may already be assigned."] = "הוספת הצוות להתראה נכשלה. ייתכן שהוא כבר משויך.",
        ["Successfully removed group '{0}' from the alarm."] = "הצוות '{0}' הוסר מההתראה בהצלחה.",
        ["Failed to remove group from alarm."] = "הסרת הצוות מההתראה נכשלה.",
        ["Successfully removed all SMS group assignments. The alarm will not send SMS notifications."] = "כל שיוכי צוותי ה־SMS הוסרו בהצלחה. ההתראה לא תשלח הודעות SMS.",
        ["Successfully assigned {0} group(s) to the alarm: {1}"] = "{0} צוותים שויכו להתראה בהצלחה: {1}",
        ["Old audit records have been cleaned up successfully."] = "רשומות הביקורת הישנות נוקו בהצלחה.",
        ["SOAP XML log cleared."] = "יומן ה־XML של SOAP נוקה.",
        ["The {0} field is required."] = "השדה {0} הוא שדה חובה.",
        ["The field {0} must be a string with a minimum length of {2} and a maximum length of {1}."] = "אורך השדה {0} חייב להיות בין {2} ל־{1} תווים.",
        ["The field {0} must be a string with a maximum length of {1}."] = "אורך השדה {0} יכול להיות עד {1} תווים.",
        ["The {0} field is not a valid e-mail address."] = "כתובת הדוא״ל אינה תקינה.",
        ["Full Name"] = "שם מלא",
        ["Phone Number"] = "מספר טלפון",
        ["Email Address (Optional)"] = "כתובת דוא״ל (אופציונלי)",
        ["Group Name"] = "שם צוות",
        ["Phone number must contain {0}-{1} digits, optionally starting with +, separated only by spaces, dashes, dots or parentheses."] = "מספר הטלפון חייב לכלול {0}–{1} ספרות, עם + אופציונלי בתחילה והפרדה רק ברווחים, מקפים, נקודות או סוגריים.",
        ["Your account ({0}) can view operational status, delivery alerts, and history. Ask an administrator to change configuration, recipients, notification teams, or routing rules."] = "החשבון שלך ({0}) יכול לצפות במצב התפעולי, בהתראות מסירה ובהיסטוריה. בקשו ממנהל לשנות הגדרות, נמענים, צוותי התראות או כללי ניתוב.",
        ["Delete User"] = "מחיקת נמען",
        ["Delete \"{0}\"? This removes them from all groups and cannot be undone."] = "למחוק את \"{0}\"? הנמען יוסר מכל הצוותים ולא ניתן לבטל פעולה זו.",
        ["Delete Group"] = "מחיקת צוות",
        ["Delete \"{0}\"? All members will be removed from the group (users themselves won't be deleted)."] = "למחוק את \"{0}\"? כל החברים יוסרו מהצוות (המשתמשים עצמם לא יימחקו).",
        ["Showing {0} of {1}"] = "מוצגות {0} מתוך {1}",
        ["Showing {0}-{1} of {2} alarms"] = "מוצגות התראות {0}–{1} מתוך {2}",
        ["Showing {0}-{1} of {2} records"] = "מוצגות רשומות {0}–{1} מתוך {2}",
        ["{0}/min"] = "{0} לדקה",
        ["A group with this name already exists."] = "כבר קיים צוות בשם זה.",
        ["Failed to create group. Please try again."] = "יצירת הצוות נכשלה. נסו שוב.",
        ["An unexpected error occurred while creating the group."] = "אירעה שגיאה לא צפויה ביצירת הצוות.",
        ["Failed to add user to group."] = "הוספת המשתמש לצוות נכשלה.",
        ["An error occurred while adding the user to the group."] = "אירעה שגיאה בהוספת המשתמש לצוות.",
        ["An error occurred while removing the user from the group."] = "אירעה שגיאה בהסרת המשתמש מהצוות.",
        ["Failed to update group. Please try again."] = "עדכון הצוות נכשל. נסו שוב.",
        ["An unexpected error occurred while updating the group."] = "אירעה שגיאה לא צפויה בעדכון הצוות.",
        ["Error loading groups. Please try again."] = "שגיאה בטעינת הצוותים. נסו שוב.",
        ["Group not deleted: could not check which SCADA alarms use it."] = "הצוות לא נמחק: לא ניתן לבדוק אילו התראות SCADA משתמשות בו.",
        ["Failed to delete group. Please try again."] = "מחיקת הצוות נכשלה. נסו שוב.",
        ["An unexpected error occurred while deleting the group."] = "אירעה שגיאה לא צפויה במחיקת הצוות.",
        ["An error occurred while adding the group."] = "אירעה שגיאה בהוספת הצוות.",
        ["An error occurred while removing the group."] = "אירעה שגיאה בהסרת הצוות.",
        ["Failed to remove SMS group assignments."] = "הסרת שיוכי צוותי ה־SMS נכשלה.",
        ["Failed to update alarm group assignments."] = "עדכון שיוכי הצוותים להתראה נכשל.",
        ["An error occurred while updating the group assignments."] = "אירעה שגיאה בעדכון שיוכי הצוותים.",
        ["Error loading audit records. Please try again."] = "שגיאה בטעינת רשומות הביקורת. נסו שוב.",
        ["Failed to cleanup old audit records."] = "ניקוי רשומות הביקורת הישנות נכשל.",
        ["An error occurred while cleaning up audit records."] = "אירעה שגיאה בניקוי רשומות הביקורת.",
        ["A user with this email address already exists."] = "כבר קיים משתמש עם כתובת דוא״ל זו.",
        ["A user with this phone number already exists."] = "כבר קיים משתמש עם מספר טלפון זה.",
        ["Failed to create user. Please check the information and try again."] = "יצירת המשתמש נכשלה. בדקו את הפרטים ונסו שוב.",
        ["An unexpected error occurred while creating the user."] = "אירעה שגיאה לא צפויה ביצירת המשתמש.",
        ["Failed to delete user. Please try again."] = "מחיקת המשתמש נכשלה. נסו שוב.",
        ["An unexpected error occurred while deleting the user."] = "אירעה שגיאה לא צפויה במחיקת המשתמש.",
        ["Failed to update user. Please check the information and try again."] = "עדכון המשתמש נכשל. בדקו את הפרטים ונסו שוב.",
        ["An unexpected error occurred while updating the user."] = "אירעה שגיאה לא צפויה בעדכון המשתמש.",
        ["Error loading users. Please try again."] = "שגיאה בטעינת המשתמשים. נסו שוב.",
        ["Failed to update SMS setting"] = "עדכון הגדרת ה־SMS נכשל",
        ["Error updating SMS setting"] = "שגיאה בעדכון הגדרת ה־SMS",
        ["Failed to update holiday setting"] = "עדכון הגדרת החג נכשל",
        ["Error updating holiday setting"] = "שגיאה בעדכון הגדרת החג",
        ["User '{0}' has been created successfully!"] = "הנמען '{0}' נוצר בהצלחה!",
        ["User '{0}' has been updated successfully!"] = "הנמען '{0}' עודכן בהצלחה!",
        ["User '{0}' has been deleted successfully!"] = "הנמען '{0}' נמחק בהצלחה!",
        ["SMS notifications enabled for {0}"] = "התראות SMS הופעלו עבור {0}",
        ["SMS notifications disabled for {0}"] = "התראות SMS הושבתו עבור {0}",
        ["Holiday work setting enabled for {0}"] = "זמינות בחגים הופעלה עבור {0}",
        ["Holiday work setting disabled for {0}"] = "זמינות בחגים הושבתה עבור {0}",
        ["Group '{0}' not deleted: {1} SCADA alarm(s) still send to it ({2}). Remove it from those alarms first (Alarms page)."] = "הצוות '{0}' לא נמחק: {1} התראות SCADA עדיין שולחות אליו ({2}). יש להסיר אותו מההתראות האלה קודם (עמוד כללי הניתוב).",
        ["Group '{0}' has been deleted successfully!"] = "הצוות '{0}' נמחק בהצלחה!",
        ["Draft discarded. The active profile is unchanged."] = "הטיוטה נמחקה. הפרופיל הפעיל לא השתנה.",
        ["Draft started from '{0}'. Fill in the URL and secrets, then verify."] = "נוצרה טיוטה מהתבנית '{0}'. השלימו את הכתובת והסודות ואז בצעו אימות.",
        ["Choose a profile file (JSON, up to 256 KB)."] = "בחרו קובץ פרופיל (JSON, עד 256 KB).",
        ["Imported '{0}' as the draft. Verify it before activating."] = "'{0}' יובא כטיוטה. יש לאמת אותו לפני ההפעלה.",
        ["Imported '{0}' as the draft. Fill in the missing values ({1} issue(s)), then verify."] = "'{0}' יובא כטיוטה. השלימו את הערכים החסרים ({1} בעיות) ואז בצעו אימות.",
        ["The {0} field is not a valid phone number."] = "מספר הטלפון אינו תקין.",
        ["Test Message"] = "הודעת בדיקה",
        ["Target Group"] = "צוות יעד",
        ["Accepted by the provider. You can activate now."] = "הספק אישר את ההודעה. אפשר להפעיל כעת.",
        ["Not accepted."] = "לא אושר.",
        ["Failure rule (checked first)"] = "כלל כישלון (נבדק ראשון)",
        ["Success rule"] = "כלל הצלחה",
        ["Failure rule"] = "כלל כישלון",
        ["Request failed: {0}"] = "הבקשה נכשלה: {0}",
        ["No provider profile is configured."] = "לא הוגדר פרופיל ספק.",
        ["Kind must be REST or SOAP."] = "סוג הספק חייב להיות REST או SOAP.",
        ["A GET request cannot have a body; put the values in the URL."] = "בקשת GET אינה יכולה לכלול גוף; יש להציב את הערכים בכתובת ה־URL.",
        ["URL is required."] = "חובה להזין כתובת URL.",
        ["Phone format Local needs a country code to strip."] = "תבנית הטלפון Local דורשת קידומת מדינה להסרה.",
        ["WS-Security authentication needs Kind SOAP."] = "אימות WS-Security דורש ספק מסוג SOAP.",
        ["SOAP providers answer HTTP 200 even when they reject a message: set a success rule on the response body."] = "ספקי SOAP עשויים להחזיר HTTP 200 גם כשהודעה נדחית; יש להגדיר כלל הצלחה על גוף התגובה.",
        ["Body is not valid XML once filled in. Check that every tag is closed and namespace prefixes are declared."] = "גוף הבקשה אינו XML תקין לאחר מילוי הערכים. יש לוודא שכל התגיות סגורות ושקידומות מרחבי השמות הוגדרו.",
        ["SOAP body must be a full envelope with an Envelope root element."] = "גוף SOAP חייב להיות מעטפת מלאה עם רכיב שורש Envelope.",
        ["Send a test SMS to all members of \"{0}\"?"] = "לשלוח הודעת SMS לבדיקה לכל החברים בצוות \"{0}\"?",
        ["Send a test SMS to \"{0}\"?"] = "לשלוח הודעת SMS לבדיקה אל \"{0}\"?",
        ["this member"] = "חבר זה",
        ["Remove {0} from this group?"] = "להסיר את {0} מהצוות?",
        ["Remove this user from the group?"] = "להסיר משתמש זה מהצוות?",
        ["Delete account {0}?"] = "למחוק את החשבון {0}?",
        ["This will refresh the holiday calendar data for the next 10 years. Continue?"] = "פעולה זו תרענן את נתוני לוח החגים ל־10 השנים הבאות. להמשיך?",
        ["Delete audit records older than 90 days?"] = "למחוק רשומות ביקורת ישנות מ־90 יום?",
        ["Activate this draft? Every alarm will use it from now on."] = "להפעיל את הטיוטה? כל ההתראות ישתמשו בה מעכשיו.",
        ["Discard the draft? The active profile is not affected."] = "למחוק את הטיוטה? הפרופיל הפעיל לא יושפע.",
        ["Replace the draft with this template?"] = "להחליף את הטיוטה בתבנית זו?",
        ["This replaces the current draft. Restore version #{0} as the draft? It will not be active until you verify and activate it."] = "פעולה זו מחליפה את הטיוטה הנוכחית. לשחזר את גרסה #{0} כטיוטה? היא לא תהיה פעילה עד שתאמתו ותפעילו אותה.",
        ["Restore version #{0} as the draft? It will not be active until you verify and activate it."] = "לשחזר את גרסה #{0} כטיוטה? היא לא תהיה פעילה עד שתאמתו ותפעילו אותה.",
        ["Select a notification team"] = "בחירת צוות התראות",
        ["SMS to {0}"] = "SMS אל {0}",
        ["Unknown User"] = "נמען לא ידוע",
        ["Too many failed attempts. Try again after {0}."] = "יותר מדי ניסיונות שנכשלו. נסו שוב לאחר {0}.",
        ["The value '{0}' is invalid."] = "הערך '{0}' אינו תקין.",
        ["The value '{0}' is not valid for {1}."] = "הערך '{0}' אינו תקין עבור {1}.",
        ["The value '{0}' is not valid."] = "הערך '{0}' אינו תקין.",
        ["The supplied value is invalid for {0}."] = "הערך שסופק אינו תקין עבור {0}.",
        ["The supplied value is invalid."] = "הערך שסופק אינו תקין.",
        ["The field {0} must be a number."] = "השדה {0} חייב להיות מספר.",
        ["The field must be a number."] = "השדה חייב להיות מספר.",
        ["A value for the '{0}' parameter or property was not provided."] = "לא סופק ערך עבור '{0}'.",
        ["Database connection successful! Found {0} users."] = "החיבור למסד הנתונים הצליח! נמצאו {0} נמענים.",
        ["Old audit records cleaned up successfully!"] = "רשומות הביקורת הישנות נוקו בהצלחה!",
        ["An error occurred during data cleanup."] = "אירעה שגיאה בניקוי הנתונים.",
        ["Account '{0}' created."] = "החשבון '{0}' נוצר.",
        ["Role updated."] = "התפקיד עודכן.",
        ["Password reset."] = "הסיסמה אופסה.",
        ["Account deleted."] = "החשבון נמחק.",
        ["User name must be 3-64 characters."] = "שם המשתמש חייב להכיל 3–64 תווים.",
        ["Unknown role."] = "תפקיד לא מוכר.",
        ["That user name is already taken."] = "שם המשתמש הזה כבר תפוס.",
        ["Account not found."] = "החשבון לא נמצא.",
        ["The last admin cannot be demoted."] = "לא ניתן להוריד את ההרשאה של המנהל האחרון.",
        ["The last admin cannot be deleted."] = "לא ניתן למחוק את המנהל האחרון."
    };

    /// <summary>
    /// Only the UI language follows the user's choice. Formatting and parsing (CurrentCulture) stay
    /// English for every request, so CSV exports, dates and posted values behave the same in Hebrew.
    /// </summary>
    public static void ConfigureRequestLocalization(RequestLocalizationOptions options)
    {
        var english = new CultureInfo("en");
        options.DefaultRequestCulture = new RequestCulture(english, english);
        options.SupportedCultures = new[] { english };
        options.SupportedUICultures = new[] { english, new CultureInfo("he-IL") };
        options.RequestCultureProviders = new IRequestCultureProvider[] { new CookieRequestCultureProvider() };
    }

    /// <summary>
    /// Model-binding errors (for example an empty value posted for a number) are built per request,
    /// so they follow the UI culture like the DataAnnotations messages.
    /// </summary>
    public static void ConfigureModelBindingMessages(MvcOptions options)
    {
        var messages = options.ModelBindingMessageProvider;
        messages.SetValueMustNotBeNullAccessor(value => Format("The value '{0}' is invalid.", value));
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => Format("The value '{0}' is not valid for {1}.", value, Localize(field)));
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => Format("The value '{0}' is not valid.", value));
        messages.SetUnknownValueIsInvalidAccessor(field => Format("The supplied value is invalid for {0}.", Localize(field)));
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => Localize("The supplied value is invalid."));
        messages.SetValueIsInvalidAccessor(value => Format("The value '{0}' is invalid.", value));
        messages.SetValueMustBeANumberAccessor(field => Format("The field {0} must be a number.", Localize(field)));
        messages.SetNonPropertyValueMustBeANumberAccessor(() => Localize("The field must be a number."));
        messages.SetMissingBindRequiredValueAccessor(field => Format("A value for the '{0}' parameter or property was not provided.", Localize(field)));
    }

    private static bool IsHebrewCulture =>
        CultureInfo.CurrentUICulture.Name.StartsWith("he", StringComparison.OrdinalIgnoreCase);

    public bool IsHebrew => IsHebrewCulture;

    public string this[string english] => Localize(english);

    public static string Localize(string english) =>
        IsHebrewCulture && Hebrew.TryGetValue(english, out var value) ? value : english;

    public static string Format(string english, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Localize(english), args);

    private static readonly (Regex Pattern, string Replacement)[] ProviderErrorPatterns =
    {
        (new(@"^Method must be one of (.+)\.$"), "השיטה חייבת להיות אחת מאלה: $1."),
        (new(@"^Variable name '(.+)' must start with a letter and use only letters, digits and _\.$"), "שם המשתנה '$1' חייב להתחיל באות ולהכיל רק אותיות, ספרות ו־_."),
        (new(@"^Variable name '(.+)' is reserved for a built-in value\.$"), "שם המשתנה '$1' שמור לערך מובנה."),
        (new(@"^Variable '(.+)' is defined twice\.$"), "המשתנה '$1' מוגדר פעמיים."),
        (new(@"^Header name '(.+)' is not a valid HTTP header name\.$"), "שם הכותרת '$1' אינו שם כותרת HTTP תקין."),
        (new(@"^Phone format must be one of (.+)\.$"), "תבנית הטלפון חייבת להיות אחת מאלה: $1."),
        (new(@"^Auth type must be one of (.+)\.$"), "סוג האימות חייב להיות אחד מאלה: $1."),
        (new(@"^(.+) authentication needs a user\.$"), "אימות $1 דורש שם משתמש."),
        (new(@"^(.+) authentication needs a password or token\.$"), "אימות $1 דורש סיסמה או אסימון."),
        (new(@"^The request never sends (.+): put it in the URL, body or a header\.$"), "הבקשה אינה שולחת את $1; יש להציב אותו בכתובת, בגוף או בכותרת."),
        (new(@"^Token (.+) uses an unknown encoding\. Use (.+)\.$"), "האסימון $1 משתמש בקידוד לא מוכר. יש להשתמש באחד מאלה: $2."),
        (new(@"^Body is not valid JSON once filled in: (.+)$"), "גוף הבקשה אינו JSON תקין לאחר מילוי הערכים: $1"),
        (new(@"^Content type or a header value is malformed: (.+)$"), "סוג התוכן או ערך כותרת אינו תקין: $1"),
        (new(@"^match type must be one of (.+)\.$"), "סוג ההתאמה חייב להיות אחד מאלה: $1."),
        (new(@"^status '(.+)' is not a code \(200\) or class \(2xx\)\.$"), "הסטטוס '$1' אינו קוד (200) או מחלקה (2xx)."),
        (new(@"^expression is required for (.+)\.$"), "חובה להזין ביטוי עבור $1."),
        (new(@"^invalid regex: (.+)$"), "ביטוי רגולרי לא תקין: $1"),
        (new(@"^JSON path must start with \$ \(e\.g\. (.+)\)\.$"), "נתיב JSON חייב להתחיל ב־$ (לדוגמה $1)."),
        (new(@"^unsupported JSON path '(.+)'\. Use (.+)\.$"), "נתיב JSON לא נתמך '$1'. יש להשתמש ב: $2."),
    };

    private static readonly Regex PasswordLengthMessage = new(@"^Password must be at least (\d+) characters\.$");

    /// <summary>Translates an <see cref="AccountService"/> result message for the current UI culture.</summary>
    public static string? LocalizeAccountMessage(string? message)
    {
        if (message is null || !IsHebrewCulture) return message;
        if (Hebrew.TryGetValue(message, out var exact)) return exact;
        var length = PasswordLengthMessage.Match(message);
        return length.Success ? $"הסיסמה חייבת להכיל לפחות {length.Groups[1].Value} תווים." : message;
    }

    private static readonly Regex ProviderRulePrefix = new(@"^(Failure rule|Success rule): (.+)$");

    /// <summary>
    /// Translates a provider-profile validation message (ProfileValidator / ResponseEvaluator)
    /// for the current UI culture. Values embedded in the message (names, tokens) are kept as-is.
    /// </summary>
    public static string LocalizeProviderError(string error)
    {
        if (!IsHebrewCulture) return error;
        var rule = ProviderRulePrefix.Match(error);
        if (rule.Success)
            return Localize(rule.Groups[1].Value) + ": " + LocalizeProviderError(rule.Groups[2].Value);
        if (Hebrew.TryGetValue(error, out var exact)) return exact;
        foreach (var (pattern, replacement) in ProviderErrorPatterns)
            if (pattern.IsMatch(error)) return pattern.Replace(error, replacement);
        return error;
    }
}
