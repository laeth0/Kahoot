| المفهوم | ماذا يعني باختصار | وضعه عندك |
|---|---|---|
| **Authentication** | إثبات من هو المستخدم | ✅ مستخدم |
| **Authorization** | هل المستخدم مسموح له بهذا الفعل؟ | ✅ مستخدم |
| **JWT Authentication** | Access Token موقّع يحمل identity/claims | ✅ مستخدم |
| **JWT Validation** | فحص signature, issuer, audience, expiry | ✅ مستخدم |
| **Token Revocation** | إبطال JWT قبل انتهاء مدته | ✅ عبر `TokenSecurityVersion` |
| **Refresh Tokens** | جلسة طويلة مع Access Tokens قصيرة | ✅ مستخدم |
| **Refresh Token Rotation** | تغيير Refresh Token بعد كل استخدام | ضمن Auth design |
| **Refresh Token Replay Detection** | اكتشاف إعادة استخدام token قديم | ضمن requirements |
| **Token Hashing** | تخزين SHA-256 للـ refresh token بدل raw token | ضمن Auth design |
| **Secure Cookies** | `HttpOnly`, `Secure`, `SameSite`, scoped Path | ✅ موجود |
| **CSRF Protection** | منع موقع آخر من استغلال cookies لإرسال requests باسم المستخدم | ✅ موجود |
| **Origin / Referer Validation** | التأكد أن cookie request جاءت من origin مسموح | ✅ موجود |
| **CORS** | تحديد أي frontend origins تستطيع قراءة API responses | ✅ موجود |
| **Password Hashing** | تخزين hash بطيء بدل password | ✅ architecture موجود |
| **Timing Attack Defense** | منع كشف وجود username من وقت الاستجابة | ضمن requirements |
| **User Enumeration Defense** | عدم كشف هل username موجود أو suspended | ضمن requirements |
| **Rate Limiting** | الحد من brute-force وabuse | ضمن Auth design |
| **Load Shedding** | رفض بعض الطلبات بدل انهيار السيرفر | ضمن requirements |
| **Least Privilege** | أقل صلاحيات ممكنة | مستخدم في Auth/CI/design |
| **IDOR Prevention** | منع الوصول لمورد مستخدم آخر بمجرد تغيير ID | ضمن architecture |
| **Foreign Resource Concealment** | resource تابع لـ tenant آخر يعامل كأنه غير موجود | ضمن requirements |
| **Multi-Tenant Isolation** | Tenant A لا يستطيع الوصول لبيانات Tenant B | أساسي في التصميم |
| **Server-derived Identity** | عدم الثقة بـ `TenantId` أو user identity من client | أساسي في التصميم |
| **Optimistic Concurrency** | اكتشاف التعديل المتزامن بدل الكتابة فوق البيانات | ✅ موجود |
| **Atomic Transactions** | مجموعة تغييرات إما كلها تنجح أو كلها تفشل | ✅ موجود |
| **Input Validation** | رفض values غير الصحيحة قبل business logic | ✅ FluentValidation |
| **Unicode Normalization / NFKC** | جعل usernames لها representation ثابتة | requirement/design |
| **XSS / Output Encoding** | منع user content من التحول إلى JavaScript | درست المفهوم ومطلوب للمحتوى |
| **Sensitive Data Redaction** | عدم logging للـ tokens/passwords | requirement |
| **Secret Management** | عدم وضع production secrets داخل Git | مستخدم في config/design |
| **RFC 7807 ProblemDetails** | errors ثابتة بدون كشف internals | ✅ موجود |
| **Keyset (Cursor-based) Pagination** | التصفح بمؤشر فريد مثل `(CreatedAt, Id)` بدل `OFFSET` لتجنب بطء الاستعلامات وسقوط السجلات | ضمن requirements (`RA-PAGE-001`) |
| **Dummy Password Hashing** | تنفيذ hash وهمي بنفس التكلفة عند تسجيل دخول مستخدم غير موجود لمنع كشف وجوده بالوقت | ضمن requirements (`AUTH-SEC-001`) |
| **Token Family Tracking** | تتبع سلالة الـ Refresh Tokens ضمن `TokenFamilyId` مع سقف زمني أقصى (30 يوماً) وإلغاء العائلة عند الاختراق | ضمن requirements (`AUTH-ROT-001`) |
| **Rotation Race Grace Window** | إعطاء نافذة أمان قصيرة (10 ثوانٍ) لمنع تسجيل خروج المستخدم عند تكرار الـ refresh المتزامن بين عدة tabs | ضمن requirements (`AUTH-ROT-002`) |
| **Progressive Backoff (No Lockout)** | تأخير استجابة تسجيل الدخول تدريجياً بدل قفل الحساب لمنع هجمات حرمان المستخدم الشرعي (DoS) | ضمن requirements (`AUTH-SEC-002`) |
| **Ephemeral Session Tokens** | جلسات مؤقتة للمشاركين (Players) بدون تسجيل، مخزنة كـ SHA-256 ومعزولة عن صلاحيات النظام | ضمن requirements (`RA-PLAY-001`) |
| **Zero Impersonation Policy** | منع الأدمن من تقمص دور الـ Host أو استعراض امتحانات وأسئلة المستأجرين الآخرين نهائياً | أساسي في التصميم (`RA-AUTHZ-002`) |
| **Fail-Closed Architecture** | إيقاف الإقلاع أو رفض الطلب فوراً عند وجود تعارض أو خطأ غير متوقع بدل العمل بوضع ضعيف أمنياً | أساسي في التصميم (`AUTH-BOOT-002`) |
| **Commit Outcome Taxonomy (A, B, C)** | تصنيف حالات فشل الشبكة والمعاملات لمعرفة متى تكون إعادة الطلب آمنة (Idempotent) | ضمن requirements (`RA-SEC-004` / Sec 6) |
| **Evidence Retention & Batch Cleanup** | الاحتفاظ بالتوكنز الملغاة لأيام كأدلة جنائية، ثم حذفها تدريجياً على دفعات صغيرة لتفادي قفل الجداول | ضمن requirements (`AUTH-CLEAN-001`) |
| **Key Identifier (`kid`) & Key Grace Transition** | تدوير مفتاح الـ JWT بسلاسة عبر قبول المفتاح القديم لفترة سماح قصيرة حتى لا تنكسر الجلسات الحالية | ضمن requirements (`AUTH-ROT-003`) |
| **Control / Zero-Width Characters Rejection** | رفض محارف التحكم والحروف الصفرية (`Cc`, `Cf`) لمنع تزييف الأسماء وكسر الواجهات | ضمن requirements (`RA-UNI-004`) |
| **Hierarchical Ownership Inheritance** | وراثة الأسئلة والخيارات والمشاركين تلقائياً لحدود الـ Tenant التابع له الـ Quiz أو الـ Game | أساسي في التصميم (`RA-OWN-002`) |
| **Composite Indexing for Tenant Isolation** | وضع `HostAccountId` كأول عمود بالفهارس المركبة لضمان سرعة الفلترة وعزل بيانات كل Tenant | ضمن requirements (`RA-ISOL-003`) |