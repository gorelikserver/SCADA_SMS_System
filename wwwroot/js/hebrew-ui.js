(() => {
  if (document.documentElement.lang !== 'he') return;

  const he = {
    'Test mode':'מצב בדיקה','Operations':'תפעול','Alarm delivery overview':'סקירת מסירת התראות',
    'Monitor the SCADA-to-provider pipeline and act on delivery exceptions.':'ניטור תהליך המסירה מ־SCADA לספק וטיפול בחריגות.',
    'Updated':'עודכן','Refresh':'רענון','Review alerts':'סקירת התראות','Delivery worker':'שירות המסירה',
    'Healthy':'תקין','Attention':'דורש טיפול','Queue':'תור','Failed deliveries':'מסירות שנכשלו',
    'Since the delivery worker started':'מאז הפעלת שירות המסירה','Queued messages':'הודעות בתור',
    'Waiting for provider processing':'ממתינות לעיבוד הספק','Accepted by provider':'אושרו על ידי הספק',
    'Live delivery log':'יומן מסירה חי','Recent activity':'פעילות אחרונה','View delivery history':'הצגת היסטוריית מסירה','Error loading recent activity':'שגיאה בטעינת הפעילות האחרונה',
    'Time':'זמן','Delivery':'מסירה','Recipient':'נמען','Status':'מצב','Loading recent activity…':'טוען פעילות אחרונה…',
    'Pipeline':'תהליך מסירה','Runtime details':'פרטי הפעלה','Rate limit':'מגבלת קצב','Deduplication':'מניעת כפילויות',
    'Last message':'הודעה אחרונה','Server time':'זמן שרת','Integration settings':'הגדרות אינטגרציה',
    'Provider, SCADA and delivery policy':'ספק, SCADA ומדיניות מסירה','Run delivery test':'הרצת בדיקת מסירה',
    'Exercise the configured route safely':'בדיקה בטוחה של הנתיב המוגדר','Routing readiness':'מוכנות ניתוב',
    'People and teams':'אנשים וצוותים','Recipients and notification teams available to routing rules.':'נמענים וצוותי התראות הזמינים לכללי הניתוב.',
    'Active recipients':'נמענים פעילים','Manage routing rules':'ניהול כללי ניתוב','No recent activity':'אין פעילות אחרונה',
    'Busy':'עמוס','Overloaded':'עומס יתר','Enabled':'פעיל','Disabled':'לא פעיל',
    'Delivery alerts':'התראות מסירה','Exceptions that can prevent a critical SCADA alarm from reaching its intended recipients.':'חריגות העלולות למנוע מהתראת SCADA קריטית להגיע לנמענים.',
    'Delivery history':'היסטוריית מסירה','Delivery pipeline is clear':'תהליך המסירה תקין',
    'Every critical alarm reached its recipients and all notification teams currently have eligible members.':'כל ההתראות הקריטיות הגיעו לנמענים ולכל צוותי ההתראות יש נמענים כשירים.',
    'Failed alarms':'התראות שנכשלו','Retries stopped or unavailable':'הניסיונות החוזרים הופסקו או אינם זמינים',
    'Retrying':'מנסה שוב','Automatic delivery still active':'המסירה האוטומטית עדיין פעילה','Integration issues':'בעיות אינטגרציה',
    'SCADA link or provider setup':'קישור SCADA או הגדרת ספק','Recipients or teams needing attention':'נמענים או צוותים הדורשים טיפול',
    'Immediate action':'פעולה מיידית','Critical alarms not delivered to everyone':'התראות קריטיות שלא נמסרו לכולם',
    'First seen':'זוהה לראשונה','SCADA message':'הודעת SCADA','Phone':'טלפון','HTTP':'HTTP','Attempts':'ניסיונות',
    'Provider response':'תגובת ספק','Operator note':'הערת מפעיל','Optional':'אופציונלי','Acknowledge alert':'אישור טיפול בהתראה',
    'Integration':'אינטגרציה','Pipeline configuration':'הגדרת תהליך המסירה','Open settings':'פתיחת הגדרות',
    'SCADA ingress link':'קישור כניסת SCADA','Provider configuration':'הגדרת ספק','Unreadable alarm file':'קובץ התראה לא קריא',
    'Find deliveries':'איתור מסירות','Filters':'מסננים','Filters active':'מסננים פעילים','Start Date':'תאריך התחלה',
    'End Date':'תאריך סיום','All Statuses':'כל המצבים','Success':'הצלחה','Failed':'נכשל','Pending':'ממתין',
    'All recipients':'כל הנמענים','Notification team':'צוות התראות','All teams':'כל הצוותים','Search':'חיפוש',
    'Total attempts':'סה״כ ניסיונות','Matching current filters':'תואם למסננים הנוכחיים','Provider accepted':'אושר על ידי הספק',
    'Requires investigation':'דורש בדיקה','Awaiting an outcome':'ממתין לתוצאה','Export CSV':'ייצוא CSV','Export Options:':'אפשרויות ייצוא:',
    'View Options:':'אפשרויות תצוגה:','Timestamp':'חותמת זמן','Alarm ID':'מזהה התראה','Description':'תיאור',
    'Team':'צוות','Response':'תגובה','No Group':'ללא צוות','No response':'אין תגובה','No delivery records found':'לא נמצאו רשומות מסירה',
    'Add recipient':'הוספת נמען','Edit recipient':'עריכת נמען','Recipient details':'פרטי נמען','Delivery readiness':'מוכנות למסירה',
    'Enable SMS Notifications':'הפעלת התראות SMS','Available on Special Days/Holidays':'זמין בימים מיוחדים ובחגים',
    'Personal Data':'פרטים אישיים','Required Information:':'מידע חובה:','Full Name:':'שם מלא:','Phone Number:':'מספר טלפון:',
    'Email:':'דוא״ל:','SMS Settings:':'הגדרות SMS:','SMS Enabled:':'SMS פעיל:','Holiday Available:':'זמינות בחגים:',
    'Special Day Settings:':'הגדרות ימים מיוחדים:','Tip:':'טיפ:','Total Users:':'סה״כ משתמשים:','Holiday Workers:':'זמינים בחגים:',
    'Recipient directory':'ספריית נמענים','Find recipients':'איתור נמענים','Search recipients':'חיפוש נמענים',
    'SMS Status':'מצב SMS','SMS Enabled':'SMS פעיל','SMS Disabled':'SMS כבוי','Contact':'פרטי קשר','Delivery eligibility':'כשירות למסירה',
    'Special days':'ימים מיוחדים','Teams':'צוותים','Actions':'פעולות','Invalid phone':'טלפון לא תקין','No recipients found':'לא נמצאו נמענים',
    'Create notification team':'יצירת צוות התראות','Create team':'יצירת צוות','Team details':'פרטי צוות','Team design guidance':'הנחיות לבניית צוות',
    'Available Users':'משתמשים זמינים','Selected Members':'חברים שנבחרו','Select All':'בחירת הכול','Select None':'ניקוי בחירה',
    'SMS Users Only':'משתמשי SMS בלבד','Common Group Types:':'סוגי צוותים נפוצים:','Best Practices:':'שיטות מומלצות:',
    'Member Selection:':'בחירת חברים:','Current Members':'חברים נוכחיים','Add Members':'הוספת חברים','Name':'שם',
    'Holiday Work':'עבודה בחגים','No Members in Group':'אין חברים בצוות','No Available Users':'אין משתמשים זמינים',
    'Team directory':'ספריית צוותים','Find notification teams':'איתור צוותי התראות',
    'Ready (has recipients)':'מוכן (יש נמענים)','Needs recipients':'נדרשים נמענים','Total teams':'סה״כ צוותים',
    'Ready teams':'צוותים מוכנים','At least one recipient':'לפחות נמען אחד','An alarm would reach nobody':'ההתראה לא תגיע לאיש',
    'Team memberships':'חברויות בצוותים','Created':'נוצר','No notification teams found':'לא נמצאו צוותי התראות',
    'Routing':'ניתוב','Routing rules':'כללי ניתוב','Matching alarms':'התראות תואמות','Across this search':'בתוצאות החיפוש',
    'Routed on this page':'מנותבות בעמוד זה','SMS teams assigned':'צוותי SMS משויכים','Delivery risks':'סיכוני מסירה',
    'Unmapped on this page':'ללא ניתוב בעמוד זה','Available teams':'צוותים זמינים','Ready to receive routes':'מוכנים לקבלת ניתובים',
    'Find a route':'איתור ניתוב','Search SCADA alarms':'חיפוש התראות SCADA','SCADA alarm':'התראת SCADA',
    'Notification teams':'צוותי התראות','Last changed':'שינוי אחרון','Route':'ניתוב','Inactive':'לא פעיל','Never modified':'לא שונה מעולם',
    'Edit routing rule':'עריכת כלל ניתוב','Save routing rule':'שמירת כלל ניתוב','Remove all teams':'הסרת כל הצוותים',
    'Coverage calendar':'לוח כיסוי','Refresh calendar':'רענון לוח שנה','Coverage window':'טווח כיסוי','View Mode':'מצב תצוגה',
    'Special Days Only':'ימים מיוחדים בלבד','Sabbatical Days Only':'ימי שבתון בלבד','All Days':'כל הימים','Days Ahead':'ימים קדימה',
    'SMS coverage rule':'כלל כיסוי SMS','Sabbatical Days':'ימי שבתון','Legend:':'מקרא:','Restricted':'מוגבל',
    'Observance':'מועד','Regular':'רגיל','Date':'תאריך','Day':'יום','Hebrew Date':'תאריך עברי','Holiday/Observance':'חג או מועד',
    'SMS Restriction':'הגבלת SMS','Work Restriction':'הגבלת עבודה','Regular Day':'יום רגיל',
    'Holiday workers only':'זמינים בחגים בלבד','All SMS-enabled users':'כל המשתמשים עם SMS','No Special Days Found':'לא נמצאו ימים מיוחדים',
    'System configuration':'הגדרות מערכת','Provider health, delivery controls, test mode and SCADA integration':'תקינות ספק, בקרות מסירה, מצב בדיקה ואינטגרציית SCADA',
    'Database':'מסד נתונים','Connected':'מחובר','Disconnected':'מנותק','Users':'משתמשים','Groups':'צוותים','Recent SMS (24h)':'SMS אחרונים (24 שעות)',
    'Last SMS':'SMS אחרון','Version':'גרסה','Environment':'סביבה','Machine':'מחשב','Uptime':'זמן פעילות','CPU cores':'ליבות מעבד','Memory':'זיכרון',
    'History':'היסטוריה','Open provider editor':'פתיחת עורך ספק','Limits and timing':'מגבלות ותזמון',
    'SCADA integration':'אינטגרציית SCADA','Save settings':'שמירת הגדרות','Configuration history':'היסטוריית הגדרות',
    'SMS Provider':'ספק SMS','Provider profile versions':'גרסאות פרופיל ספק','Restore as draft':'שחזור כטיוטה','Profile':'פרופיל',
    'Activated':'הופעל','Verified':'אומת','Active':'פעיל','Export':'ייצוא','Recent changes':'שינויים אחרונים','When':'מתי','Who':'מי','Area':'תחום','Action':'פעולה','Details':'פרטים',
    'History & rollback':'היסטוריה ושחזור','Active profile — used for every alarm':'פרופיל פעיל — משמש לכל התראה','No active profile.':'אין פרופיל פעיל.',
    'Draft':'טיוטה','Activate':'הפעלה','Discard draft':'מחיקת טיוטה','Export draft':'ייצוא טיוטה','Import':'ייבוא',
    'Start from a template':'התחלה מתבנית','Use':'שימוש','Kind':'סוג','Method':'שיטה','URL':'כתובת URL','Content type':'סוג תוכן',
    'Body':'גוף','Headers':'כותרות','Variables (constants and secrets)':'משתנים (קבועים וסודות)','Secret':'סוד','Authentication':'אימות',
    'Phone number format':'תבנית מספר טלפון','Save draft':'שמירת טיוטה','Preview request':'תצוגה מקדימה של בקשה','Draft problems':'בעיות בטיוטה',
    'Verify with a real send':'אימות באמצעות שליחה אמיתית','Save and send test SMS':'שמירה ושליחת SMS לבדיקה','Tokens':'אסימונים','Write':'כתיבה',
    'Delivery test':'בדיקת מסירה','Provider editor':'עורך ספק','Result':'תוצאה','Detail':'פרטים','No members':'אין חברים',
    'Access management':'ניהול גישה','Role':'תפקיד','Last sign-in':'כניסה אחרונה','Reset password':'איפוס סיסמה','Set':'הגדרה',
    'Add operator account':'הוספת חשבון מפעיל','Add':'הוספה','About AlarmBridge':'אודות AlarmBridge','SCADA alarm delivery platform':'פלטפורמת מסירת התראות SCADA',
    'Key Features:':'יכולות עיקריות:','Technical Specifications:':'מפרט טכני:','Integration Guide':'מדריך אינטגרציה',
    'On this page':'בעמוד זה','Parameters':'פרמטרים','Examples':'דוגמאות','Dynamic keywords':'מילות מפתח דינמיות',
    'Config examples':'דוגמאות הגדרה','Placeholders':'מצייני מקום','Auth types':'סוגי אימות','Template examples':'דוגמאות תבנית',
    'Troubleshooting':'פתרון תקלות','For system integrators and technical operators':'למשלבי מערכות ולמפעילים טכניים',
    "We couldn't complete that request":'לא הצלחנו להשלים את הבקשה','Request ID:':'מזהה בקשה:','Return to overview':'חזרה לסקירה הכללית',
    'AlarmBridge encountered an unexpected problem. Try again, or give the request ID below to your administrator.':'AlarmBridge נתקל בבעיה לא צפויה. נסו שוב, או מסרו את מזהה הבקשה למנהל המערכת.',
    'Acknowledging removes an alarm from the banner; it never stops automatic retries.':'אישור הטיפול מסיר את ההתראה מהכרזה; הוא אינו מפסיק ניסיונות חוזרים אוטומטיים.',
    'No delivery attempts were recorded':'לא נרשמו ניסיונות מסירה','The team had no eligible recipients, or delivery state could not be stored.':'לצוות לא היו נמענים כשירים, או שלא היה ניתן לשמור את מצב המסירה.',
    'Example: called the on-call engineer':'לדוגמה: התקשרתי למהנדס הכונן','Recipients and notification teams':'נמענים וצוותי התראות','Correct these issues before the next alarm uses the affected route.':'תקנו בעיות אלה לפני שההתראה הבאה תשתמש בנתיב המושפע.',
    'No delivery attempts match the current filters. Try broadening the search or date range.':'אין ניסיונות מסירה התואמים למסננים הנוכחיים. נסו להרחיב את החיפוש או את טווח התאריכים.','Delivery attempts will appear here after the first SCADA alarm is processed.':'ניסיונות מסירה יופיעו כאן לאחר עיבוד התראת SCADA הראשונה.',
    'Search by alarm ID, description, user name, group, or phone...':'חיפוש לפי מזהה התראה, תיאור, שם משתמש, צוות או טלפון...','Time filters':'מסנני זמן','Status filters':'מסנני מצב','Filtered delivery summary':'סיכום מסירות מסונן','First':'ראשון','Previous':'הקודם','Next':'הבא','Last':'אחרון',
    'People who can receive SCADA alarm notifications through configured routing rules.':'אנשים שיכולים לקבל התראות SCADA באמצעות כללי הניתוב שהוגדרו.','Add the first person who should receive routed alarms.':'הוסיפו את האדם הראשון שצריך לקבל התראות מנותבות.','No recipients match the current filters.':'אין נמענים התואמים למסננים הנוכחיים.','Mobile number for SMS alerts (required)':'מספר נייד להתראות SMS (חובה)','Email address for notifications (optional)':'כתובת דוא״ל להתראות (אופציונלי)',
    'Organize recipients into the teams targeted by SCADA routing rules.':'ארגנו נמענים בצוותים אליהם מפנים כללי הניתוב של SCADA.','Create a team before assigning it to a SCADA routing rule.':'צרו צוות לפני שיוך שלו לכלל ניתוב SCADA.','No teams match the current filters.':'אין צוותים התואמים למסננים הנוכחיים.','Add users to this group to start sending SMS notifications.':'הוסיפו משתמשים לצוות כדי להתחיל לשלוח התראות SMS.','Create new users to add them to this group.':'צרו משתמשים חדשים כדי להוסיף אותם לצוות.','No users available.':'אין משתמשים זמינים.','No members selected yet.':'עדיין לא נבחרו חברים.',
    'Connect every SCADA alarm to the notification teams responsible for receiving its SMS alerts.':'חברו כל התראת SCADA לצוותי ההתראות האחראים לקבלת הודעות ה־SMS שלה.','No notification teams are selected. Other alarm actions remain unchanged.':'לא נבחרו צוותי התראות. פעולות ההתראה האחרות יישארו ללא שינוי.','SMS delivery will be disabled for this alarm.':'מסירת SMS תושבת עבור התראה זו.',
    'Real-time SMS notifications':'התראות SMS בזמן אמת','User and group management':'ניהול משתמשים וצוותים','Holiday awareness (Jewish calendar)':'התחשבות בחגים (לוח עברי)','Complete audit trail':'נתיב ביקורת מלא','Rate limiting and deduplication':'הגבלת קצב ומניעת כפילויות','Professional web dashboard':'לוח בקרה מקצועי','RESTful API integration':'אינטגרציית API מסוג REST','Framework:':'תשתית:','Web Framework:':'תשתית אינטרנט:','Database:':'מסד נתונים:','ORM:':'ORM:','UI Framework:':'תשתית ממשק:','Background Service:':'שירות רקע:','Operating System:':'מערכת הפעלה:','System Version:':'גרסת מערכת:','Build Date:':'תאריך בנייה:','Machine Name:':'שם מחשב:','OS Version:':'גרסת מערכת הפעלה:','Current Time:':'השעה הנוכחית:',
    'AlarmBridge connects your SCADA platform to your SMS provider. The integration has two legs:':'AlarmBridge מחברת את פלטפורמת ה־SCADA לספק ה־SMS. לאינטגרציה שני חלקים:','SCADA → App: the bat file':'SCADA → היישום: קובץ ה־bat','REST Provider':'ספק REST','SOAP Provider':'ספק SOAP','Keyword(s)':'מילות מפתח','Replaced with':'מוחלף ב־','Type':'סוג','Required':'חובה','Example':'דוגמה','Position':'מיקום','Variable':'משתנה','Placeholder':'מציין מקום','Resolved to':'מומר ל־','Source':'מקור','Mode':'מצב','What it sends':'מה נשלח','When to use':'מתי להשתמש','Yes':'כן','No':'לא','Dynamic':'דינמי','Credential':'פרט הזדהות','Static':'קבוע',
    'Alarm description text. Supports Hebrew / UTF-8.':'טקסט תיאור ההתראה. תומך בעברית וב־UTF-8.','Numeric ID of the SMS group to notify. Must be a positive integer.':'מזהה מספרי של צוות ה־SMS שיקבל הודעה. חייב להיות מספר שלם חיובי.','Sensor value or extra context. Appended to MESSAGE as MESSAGE - VALUE.':'ערך חיישן או הקשר נוסף. מתווסף ל־MESSAGE בתבנית MESSAGE - VALUE.','Unique alarm identifier for deduplication and audit. Auto-generated if omitted.':'מזהה התראה ייחודי למניעת כפילויות ולביקורת. נוצר אוטומטית אם הושמט.','Text priority label stored in the audit log. Defaults to normal.':'תווית עדיפות טקסטואלית הנשמרת ביומן הביקורת. ברירת המחדל היא normal.',
    'Wrong user name or password.':'שם המשתמש או הסיסמה שגויים.','The UserName field is required.':'שדה שם המשתמש הוא שדה חובה.','The Password field is required.':'שדה הסיסמה הוא שדה חובה.','The field is required.':'זהו שדה חובה.','Passwords do not match.':'הסיסמאות אינן תואמות.','Current password is wrong.':'הסיסמה הנוכחית שגויה.','Too many failed attempts; try again later.':'יותר מדי ניסיונות שנכשלו; נסו שוב מאוחר יותר.','Too many failed attempts. Try again later.':'יותר מדי ניסיונות שנכשלו. נסו שוב מאוחר יותר.',
    'Error loading groups. Please try again.':'שגיאה בטעינת הצוותים. נסו שוב.','Failed to delete group. Please try again.':'מחיקת הצוות נכשלה. נסו שוב.','An unexpected error occurred while deleting the group.':'אירעה שגיאה לא צפויה במחיקת הצוות.','A group with this name already exists.':'כבר קיים צוות בשם זה.','Failed to create group. Please try again.':'יצירת הצוות נכשלה. נסו שוב.','Failed to update group. Please try again.':'עדכון הצוות נכשל. נסו שוב.','An unexpected error occurred while creating the group.':'אירעה שגיאה לא צפויה ביצירת הצוות.','An unexpected error occurred while updating the group.':'אירעה שגיאה לא צפויה בעדכון הצוות.',
    'Error loading users. Please try again.':'שגיאה בטעינת המשתמשים. נסו שוב.','Failed to update SMS setting':'עדכון הגדרת ה־SMS נכשל','Error updating SMS setting':'שגיאה בעדכון הגדרת ה־SMS','Failed to update holiday setting':'עדכון הגדרת החג נכשל','Error updating holiday setting':'שגיאה בעדכון הגדרת החג','A user with this email address already exists.':'כבר קיים משתמש עם כתובת דוא״ל זו.','A user with this phone number already exists.':'כבר קיים משתמש עם מספר טלפון זה.','Failed to delete user. Please try again.':'מחיקת המשתמש נכשלה. נסו שוב.','An unexpected error occurred while deleting the user.':'אירעה שגיאה לא צפויה במחיקת המשתמש.',
    'Error loading calendar data. Please try again.':'שגיאה בטעינת נתוני הלוח. נסו שוב.','Holiday calendar has been refreshed successfully!':'לוח החגים רוענן בהצלחה!','Failed to refresh calendar data.':'רענון נתוני הלוח נכשל.','Could not load the settings history. Check the database connection.':'לא ניתן לטעון את היסטוריית ההגדרות. בדקו את החיבור למסד הנתונים.','Error loading system settings. Please try again.':'שגיאה בטעינת הגדרות המערכת. נסו שוב.','Settings saved. Changes are active immediately.':'ההגדרות נשמרו. השינויים פעילים מייד.','Database connection test failed. Check your connection string.':'בדיקת החיבור למסד הנתונים נכשלה. בדקו את מחרוזת החיבור.','Error loading audit records. Please try again.':'שגיאה בטעינת רשומות הביקורת. נסו שוב.','You cannot delete your own account.':'לא ניתן למחוק את החשבון שלך.','Save failed.':'השמירה נכשלה.','Settings saved.':'ההגדרות נשמרו.','Recipient created.':'הנמען נוצר.','Recipient updated.':'הנמען עודכן.','Recipient deleted.':'הנמען נמחק.','Team created.':'הצוות נוצר.','Team updated.':'הצוות עודכן.','Team deleted.':'הצוות נמחק.',
    'Disable SMS delivery?':'להשבית מסירת SMS?','No notification teams are selected. Saving will disable SMS delivery for this alarm; other alarm actions will remain unchanged.':'לא נבחרו צוותי התראות. השמירה תשבית את מסירת ה־SMS עבור התראה זו; פעולות ההתראה האחרות יישארו ללא שינוי.','Disable SMS delivery':'השבתת מסירת SMS','Cleanup Old Records':'ניקוי רשומות ישנות','Delete audit records older than 90 days? This cannot be undone.':'למחוק רשומות ביקורת ישנות מ־90 יום? לא ניתן לבטל פעולה זו.','Delete Records':'מחיקת רשומות','Refresh Holiday Calendar':'רענון לוח החגים','This will refresh the holiday calendar data for the next 10 years. Continue?':'פעולה זו תרענן את נתוני לוח החגים ל־10 השנים הבאות. להמשיך?','Send Test SMS':'שליחת SMS לבדיקה','Send SMS':'שליחת SMS','Delete Old Audit Records':'מחיקת רשומות ביקורת ישנות','This will permanently delete all SMS audit records older than 90 days. This action cannot be undone.':'פעולה זו תמחק לצמיתות את כל רשומות ביקורת ה־SMS הישנות מ־90 יום. לא ניתן לבטל פעולה זו.','Delete':'מחיקה','Remove Group Member':'הסרת חבר צוות','Remove':'הסרה','Processing...':'מעבד...',
    'Unknown team':'צוות לא ידוע','is not a deliverable 7–15 digit number.':'אינו מספר תקין למסירה באורך 7–15 ספרות.','Never':'אף פעם','Refreshing...':'מרענן...','Updating Group...':'מעדכן צוות...','Adding...':'מוסיף...','Creating...':'יוצר...','Saving...':'שומר...','Update User':'עדכון נמען','(SMS off)':'(SMS כבוי)','SCADA alarm:':'התראת SCADA:','Select the notification teams that should receive SMS alerts when this alarm is triggered.':'בחרו את צוותי ההתראות שיקבלו הודעות SMS כאשר התראה זו מופעלת.'
  };

  const patterns = [
    [/^(\d+) per page$/, '$1 בכל עמוד'],
    [/^Page (\d+) of (\d+)$/, 'עמוד $1 מתוך $2'],
    [/^Recipient #(\d+)$/, 'נמען #$1'],
    [/^Team #(\d+)$/, 'צוות #$1'],
    [/^(\d+) Days$/, '$1 ימים'],
    [/^(\d+) Year$/, '$1 שנה'],
    [/^(\d+) Years$/, '$1 שנים'],
    [/^(\d+) of (\d+) recipients accepted by the provider$/, '$1 מתוך $2 נמענים אושרו על ידי הספק'],
    [/^Team #(\d+) has no eligible recipients right now\.$/, 'לצוות #$1 אין כרגע נמענים כשירים.']
  ];
  // Text inside these elements is application data or code, never UI copy: alarm descriptions,
  // SCADA messages, provider responses, names, payloads. Mark data cells with data-no-translate.
  const protectedText = 'script,style,code,pre,textarea,.font-monospace,.pv-mono,[data-no-translate]';
  const translatedAttributes = ['placeholder','title','aria-label','data-confirm','data-bs-title'];
  const translate = value => {
    const trimmed = value.trim();
    if (!trimmed) return value;
    let translated = Object.prototype.hasOwnProperty.call(he, trimmed) ? he[trimmed] : null;
    if (!translated) for (const [pattern, replacement] of patterns) if (pattern.test(trimmed)) { translated = trimmed.replace(pattern, replacement); break; }
    return translated ? value.replace(trimmed, () => translated) : value;
  };
  const canTranslateText = node => {
    const parent = node.parentElement;
    if (!parent || parent.closest(protectedText)) return false;
    // An <option> without a value attribute posts its text; translating it would change the posted value.
    const option = parent.closest('option');
    return !option || option.hasAttribute('value');
  };
  const walk = root => {
    if (root.nodeType === Node.TEXT_NODE) {
      if (canTranslateText(root)) {
        const translated = translate(root.nodeValue);
        // Write only on change: an unchanged write still queues a characterData record.
        if (translated !== root.nodeValue) root.nodeValue = translated;
      }
      return;
    }
    if (!(root instanceof Element) || root.closest('script,style,[data-no-translate]')) return;
    for (const attr of translatedAttributes) {
      if (!root.hasAttribute(attr)) continue;
      const current = root.getAttribute(attr);
      const translated = translate(current);
      if (translated !== current) root.setAttribute(attr, translated);
    }
    if (root.matches('textarea,input')) return;
    for (const node of root.childNodes) walk(node);
  };
  walk(document.documentElement);
  const observer = new MutationObserver(records => {
    for (const record of records) {
      if (record.type === 'childList') record.addedNodes.forEach(walk);
      else if (record.target.isConnected) walk(record.target);
    }
    // Discard the records our own writes just queued so they never trigger another pass.
    observer.takeRecords();
  });
  observer.observe(document.body, {
    childList:true, subtree:true, characterData:true, attributes:true,
    attributeFilter:translatedAttributes
  });
})();
