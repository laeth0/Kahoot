| المفهوم | ماذا يعني باختصار |
|---|---|
| **Authentication** | إثبات من هو المستخدم |
| **Authorization** | هل المستخدم مسموح له بهذا الفعل؟ |
| **JWT Authentication** | Access Token موقّع يحمل identity/claims |
| **JWT Validation** | فحص signature, issuer, audience, expiry |
| **Token Revocation** | إبطال JWT قبل انتهاء مدته |
| **Refresh Tokens** | جلسة طويلة مع Access Tokens قصيرة |
| **Refresh Token Rotation** | تغيير Refresh Token بعد كل استخدام |
| **Refresh Token Replay Detection** | اكتشاف إعادة استخدام token قديم |
| **Token Hashing** | تخزين SHA-256 للـ refresh token بدل raw token |
| **Secure Cookies** | `HttpOnly`, `Secure`, `SameSite`, scoped Path |
| **CSRF Protection** | منع موقع آخر من استغلال cookies لإرسال requests باسم المستخدم |
| **Origin / Referer Validation** | التأكد أن cookie request جاءت من origin مسموح |
| **CORS** | تحديد أي frontend origins تستطيع قراءة API responses |
| **Password Hashing** | تخزين hash بطيء بدل password |
| **Timing Attack Defense** | منع كشف وجود username من وقت الاستجابة |
| **User Enumeration Defense** | عدم كشف هل username موجود أو suspended |
| **Rate Limiting** | الحد من brute-force وabuse |
| **Load Shedding** | رفض بعض الطلبات بدل انهيار السيرفر |
| **Least Privilege** | أقل صلاحيات ممكنة |
| **IDOR Prevention** | منع الوصول لمورد مستخدم آخر بمجرد تغيير ID |
| **Foreign Resource Concealment** | resource تابع لـ tenant آخر يعامل كأنه غير موجود |
| **Multi-Tenant Isolation** | Tenant A لا يستطيع الوصول لبيانات Tenant B |
| **Server-derived Identity** | عدم الثقة بـ `TenantId` أو user identity من client |
| **Optimistic Concurrency** | اكتشاف التعديل المتزامن بدل الكتابة فوق البيانات |
| **Atomic Transactions** | مجموعة تغييرات إما كلها تنجح أو كلها تفشل |
| **Input Validation** | رفض values غير الصحيحة قبل business logic |
| **Unicode Normalization / NFKC** | جعل usernames لها representation ثابتة |
| **XSS / Output Encoding** | منع user content من التحول إلى JavaScript |
| **Sensitive Data Redaction** | عدم logging للـ tokens/passwords |
| **Secret Management** | عدم وضع production secrets داخل Git |
| **RFC 7807 ProblemDetails** | errors ثابتة بدون كشف internals |
| **Keyset (Cursor-based) Pagination** | التصفح بمؤشر فريد مثل `(CreatedAt, Id)` بدل `OFFSET` لتجنب بطء الاستعلامات وسقوط السجلات |
| **Dummy Password Hashing** | تنفيذ hash وهمي بنفس التكلفة عند تسجيل دخول مستخدم غير موجود لمنع كشف وجوده بالوقت |
| **Token Family Tracking** | تتبع سلالة الـ Refresh Tokens ضمن `TokenFamilyId` مع سقف زمني أقصى (30 يوماً) وإلغاء العائلة عند الاختراق |
| **Rotation Race Grace Window** | إعطاء نافذة أمان قصيرة (10 ثوانٍ) لمنع تسجيل خروج المستخدم عند تكرار الـ refresh المتزامن بين عدة tabs |
| **Progressive Backoff (No Lockout)** | تأخير استجابة تسجيل الدخول تدريجياً بدل قفل الحساب لمنع هجمات حرمان المستخدم الشرعي (DoS) |
| **Ephemeral Session Tokens** | جلسات مؤقتة للمشاركين (Players) بدون تسجيل، مخزنة كـ SHA-256 ومعزولة عن صلاحيات النظام |
| **Zero Impersonation Policy** | منع الأدمن من تقمص دور الـ Host أو استعراض امتحانات وأسئلة المستأجرين الآخرين نهائياً |
| **Fail-Closed Architecture** | إيقاف الإقلاع أو رفض الطلب فوراً عند وجود تعارض أو خطأ غير متوقع بدل العمل بوضع ضعيف أمنياً |
| **Commit Outcome Taxonomy (A, B, C)** | تصنيف حالات فشل الشبكة والمعاملات لمعرفة متى تكون إعادة الطلب آمنة (Idempotent) |
| **Evidence Retention & Batch Cleanup** | الاحتفاظ بالتوكنز الملغاة لأيام كأدلة جنائية، ثم حذفها تدريجياً على دفعات صغيرة لتفادي قفل الجداول |
| **Key Identifier (`kid`) & Key Grace Transition** | تدوير مفتاح الـ JWT بسلاسة عبر قبول المفتاح القديم لفترة سماح قصيرة حتى لا تنكسر الجلسات الحالية |
| **Control / Zero-Width Characters Rejection** | رفض محارف التحكم والحروف الصفرية (`Cc`, `Cf`) لمنع تزييف الأسماء وكسر الواجهات |
| **Hierarchical Ownership Inheritance** | وراثة الأسئلة والخيارات والمشاركين تلقائياً لحدود الـ Tenant التابع له الـ Quiz أو الـ Game |
| **Composite Indexing for Tenant Isolation** | وضع `HostAccountId` كأول عمود بالفهارس المركبة لضمان سرعة الفلترة وعزل بيانات كل Tenant |