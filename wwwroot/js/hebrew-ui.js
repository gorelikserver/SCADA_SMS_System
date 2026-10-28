(() => {
  if (document.documentElement.lang !== 'he') return;

  const he = {
    'Test mode':'מצב בדיקה','Operations':'תפעול','Alarm delivery overview':'סקירת מסירת התראות',
    'Monitor the SCADA-to-provider pipeline and act on delivery exceptions.':'ניטור תהליך המסירה מ־SCADA לספק וטיפול בחריגות.',
    'Updated':'עודכן','Refresh':'רענון','Review alerts':'סקירת התראות','Delivery worker':'שירות המסירה',
    'Healthy':'תקין','Attention':'דורש טיפול','Queue':'תור','Failed deliveries':'מסירות שנכשלו',
    'Since the delivery worker started':'מאז הפעלת שירות המסירה','Queued messages':'הודעות בתור',
    'Waiting for provider processing':'ממתינות לעיבוד הספק','Accepted by provider':'אושרו על ידי הספק',
    'Live delivery log':'יומן מסירה חי','Recent activity':'פעילות אחרונה','View delivery history':'הצגת היסטוריית מסירה',
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
    'Add recipient':'הוספת נמען','Recipient directory':'ספריית נמענים','Find recipients':'איתור נמענים','Search recipients':'חיפוש נמענים',
    'SMS Status':'מצב SMS','SMS Enabled':'SMS פעיל','SMS Disabled':'SMS כבוי','Contact':'פרטי קשר','Delivery eligibility':'כשירות למסירה',
    'Special days':'ימים מיוחדים','Teams':'צוותים','Actions':'פעולות','Invalid phone':'טלפון לא תקין','No recipients found':'לא נמצאו נמענים',
    'Create notification team':'יצירת צוות התראות','Create team':'יצירת צוות','Team details':'פרטי צוות','Team design guidance':'הנחיות לבניית צוות',
    'Available Users':'משתמשים זמינים','Selected Members':'חברים שנבחרו','Select All':'בחירת הכול','Select None':'ניקוי בחירה',
    'SMS Users Only':'משתמשי SMS בלבד','Common Group Types:':'סוגי צוותים נפוצים:','Best Practices:':'שיטות מומלצות:',
    'Member Selection:':'בחירת חברים:','Current Members':'חברים נוכחיים','Add Members':'הוספת חברים','Name':'שם',
    'Holiday Work':'עבודה בחגים','No Members in Group':'אין חברים בצוות','No Available Users':'אין משתמשים זמינים',
    'Team directory':'ספריית צוותים','Find notification teams':'איתור צוותי התראות','All teams':'כל הצוותים',
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
    'SMS Restriction':'הגבלת SMS','Available Users':'משתמשים זמינים','Work Restriction':'הגבלת עבודה','Regular Day':'יום רגיל',
    'Holiday workers only':'זמינים בחגים בלבד','All SMS-enabled users':'כל המשתמשים עם SMS','No Special Days Found':'לא נמצאו ימים מיוחדים',
    'System configuration':'הגדרות מערכת','Provider health, delivery controls, test mode and SCADA integration':'תקינות ספק, בקרות מסירה, מצב בדיקה ואינטגרציית SCADA',
    'Database':'מסד נתונים','Connected':'מחובר','Disconnected':'מנותק','Users':'משתמשים','Groups':'צוותים','Recent SMS (24h)':'SMS אחרונים (24 שעות)',
    'Last SMS':'SMS אחרון','Version':'גרסה','Environment':'סביבה','Machine':'מחשב','Uptime':'זמן פעילות','CPU cores':'ליבות מעבד','Memory':'זיכרון',
    'History':'היסטוריה','Open provider editor':'פתיחת עורך ספק','Test mode':'מצב בדיקה','Limits and timing':'מגבלות ותזמון',
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
    "We couldn't complete that request":'לא הצלחנו להשלים את הבקשה','Request ID:':'מזהה בקשה:','Return to overview':'חזרה לסקירה הכללית'
  };

  const patterns = [
    [/^(\d+) per page$/, '$1 בכל עמוד'],
    [/^Page (\d+) of (\d+)$/, 'עמוד $1 מתוך $2'],
    [/^Recipient #(\d+)$/, 'נמען #$1'],
    [/^Team #(\d+)$/, 'צוות #$1'],
    [/^(\d+) Days$/, '$1 ימים'],
    [/^(\d+) Year$/, '$1 שנה']
  ];
  const skip = 'script,style,code,pre,.font-monospace,.pv-mono,[data-no-translate]';
  const translate = value => {
    const trimmed = value.trim();
    if (!trimmed) return value;
    let translated = he[trimmed];
    if (!translated) for (const [pattern, replacement] of patterns) if (pattern.test(trimmed)) { translated = trimmed.replace(pattern, replacement); break; }
    return translated ? value.replace(trimmed, translated) : value;
  };
  const walk = root => {
    if (root.nodeType === Node.TEXT_NODE) {
      if (!root.parentElement?.closest(skip)) root.nodeValue = translate(root.nodeValue);
      return;
    }
    if (!(root instanceof Element) || root.matches(skip)) return;
    for (const attr of ['placeholder','title','aria-label']) if (root.hasAttribute(attr)) root.setAttribute(attr, translate(root.getAttribute(attr)));
    for (const node of root.childNodes) walk(node);
  };
  walk(document.body);
  new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(walk)))
    .observe(document.body, { childList:true, subtree:true });
})();
