# COMPLETED_TASKS

آخرین به‌روزرسانی: 2026-11-10

## DONE-001 — فراموشی رمز عبور با بله (ShopAdmin + ShopAccount)
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - تولید رمز موقت تصادفی امن (`PasswordHelper.GenerateTemporaryPassword`).
  - بازنشانی رمز مشتری با موبایل و ارسال مستقیم به `BaleChatId`.
  - بازنشانی رمز مدیر فروشگاه با موبایل مالک و ارسال به گیرنده‌های بله متصل.
  - افزودن فرم «فراموشی رمز» در لاگین مشتری و لاگین مدیریت فروشگاه.
  - افزودن `BaleChatId` به مدل مشتری و فرم‌های ثبت‌نام/پروفایل.
- فایل‌های کلیدی:
  - `Controllers/ShopAccountController.cs`
  - `Controllers/ShopAdminController.cs`
  - `Services/BaleNotifier.cs`
  - `Services/PasswordHelper.cs`
  - `Models/Store/StoreModels.cs`
  - `Views/ShopAccount/Login.cshtml`
  - `Views/ShopAccount/Register.cshtml`
  - `Views/ShopAccount/Profile.cshtml`
  - `Views/ShopAdmin/Login.cshtml`

## DONE-002 — فراموشی رمز عبور برای Admin اصلی
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - افزودن اکشن `ForgotPassword` در `AdminController`.
  - تولید رمز موقت، تغییر credential ادمین، ارسال رمز در بله (SiteScope).
  - افزودن فرم «فراموشی رمز» در `Views/Admin/Login.cshtml`.
- فایل‌های کلیدی:
  - `Controllers/AdminController.cs`
  - `Views/Admin/Login.cshtml`

## DONE-003 — ایجاد مستندات تحلیل محصول/تبدیل
- وضعیت: تکمیل‌شده
- خروجی‌ها:
  - `docs/seo-conversion-roadmap.md`
  - `docs/ux-conversion-audit.md`
  - `docs/usability-complexity-reduction-fa.md`
  - `docs/customer-journey-friction-fa.md`
  - `docs/audience-first-visit-analysis-fa.md`
  - `docs/github-issues-top10-backlog.md`

## DONE-004 — تکمیل BL-001: بازنویسی CTAهای نتیجه‌محور برای افزایش ثبت‌نام آزمایشی
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - بازنویسی CTAهای مسیر اصلی کاربر جدید (Home → Products → Product Details → Signup) با تمرکز روی «شروع آزمایشی رایگان».
  - حذف/کاهش متن‌های مبهم مانند «خرید» در نقاط کشف اولیه محصول و جایگزینی با «جزئیات و قیمت».
  - تقویت مسیر تصمیم‌گیری: «اول دمو را ببینید» و سپس «شروع آزمایشی».
- خودارزیابی اثر:
  - کاهش ابهام قدم بعدی برای بازدیدکننده جدید.
  - افزایش وضوح پیام ارزشی و احتمال رشد ثبت‌نام آزمایشی در 30 روز آینده.
  - ریسک پایین (فقط تغییر متن، بدون تغییر منطق تجاری).
- فایل‌های کلیدی:
  - `Views/Home/Index.cshtml`
  - `Views/Products/Index.cshtml`
  - `Views/Products/Details.cshtml`
  - `Views/Signup/Index.cshtml`

## DONE-005 — تکمیل BL-002: تفکیک واضح مسیرهای ورود نقش‌ها
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - شفاف‌سازی ورودی نقش‌ها در هدر سایت اصلی با برچسب دقیق‌تر فروشنده و لینک مدیر سیستم.
  - افزودن راهنمای نقش در صفحات لاگین: فروشنده، مشتری، مدیر سیستم، مدیر فروشگاه.
  - افزودن لینک‌های اصلاح مسیر بین صفحات لاگین برای کاهش ورود اشتباه.
- خودارزیابی اثر:
  - کاهش سردرگمی اولین ورود و خطای انتخاب صفحه لاگین.
  - بهبود مستقیم نرخ موفقیت ورود و شروع مسیر ثبت‌نام/خرید.
  - ریسک فنی پایین (فقط متن و لینک راهنما، بدون تغییر منطق احراز هویت).
- فایل‌های کلیدی:
  - `Views/Shared/_Layout.cshtml`
  - `Views/Signup/Login.cshtml`
  - `Views/Admin/Login.cshtml`
  - `Views/ShopAccount/Login.cshtml`
  - `Views/ShopAdmin/Login.cshtml`

## DONE-006 — اصلاح فوری UI لاگین مدیران کسب‌وکار
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - رفع مشکل نمایش باکس خالی خطا در لاگین مدیران کسب‌وکار با تغییر `ValidationSummary(true)` به `ValidationSummary(false)`.
  - تغییر واژگان فروشنده/فروشگاه به واژگان عمومی‌تر برای همه کسب‌وکارها.
  - به‌روزرسانی عنوان منوی هدر از «ورود فروشنده (مدیر فروشگاه)» به «ورود مدیر کسب‌وکار».
  - به‌روزرسانی CTA هدر از «ساخت فروشگاه رایگان» به «ساخت سایت رایگان».
- فایل‌های کلیدی:
  - `Views/Signup/Login.cshtml`
  - `Views/Shared/_Layout.cshtml`

## DONE-007 — هدایت لاگین اولیه مدیران کسب‌وکار به لاگین همان سایت
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - ذخیره آخرین `company` در زمان خروج از پنل ShopAdmin با کوکی `nk_last_company`.
  - در `Signup/Login` اگر `company` از QueryString یا کوکی موجود باشد، کاربر مستقیم به `/ShopAdmin/{company}/Login` هدایت شود.
  - برای کاربر جدیدی که هنوز company ندارد، فرم ورود عمومی مدیران کسب‌وکار همچنان نمایش داده می‌شود.
- اثر:
  - هم‌راستایی لاگین اولیه با صفحه لاگین خروج (همان صفحه‌ای که کاربر می‌شناسد).
  - کاهش اصطکاک و رفت‌وبرگشت غیرضروری بین صفحات ورود.
- فایل‌های کلیدی:
  - `Controllers/SignupController.cs`
  - `Controllers/ShopAdminController.cs`

## DONE-008 — اصلاح مسیر لینک «ورود مدیر کسب‌وکار» با company آخر
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - لینک «ورود مدیر کسب‌وکار» در هدر، در صورت وجود کوکی `nk_last_company` مستقیماً به `ShopAdmin/{company}/Login` می‌رود.
  - هنگام باز شدن صفحه لاگین ShopAdmin نیز کوکی آخرین company به‌روزرسانی می‌شود تا مسیر ورود بعدی دقیق باشد.
- فایل‌های کلیدی:
  - `Views/Shared/_Layout.cshtml`
  - `Controllers/ShopAdminController.cs`

## DONE-009 — حذف فرم `/Signup/Login` و هدایت صرف به لاگین اختصاصی کسب‌وکار
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - اکشن GET مسیر `Signup/Login` دیگر صفحه فرم را نمایش نمی‌دهد.
  - اگر `company` معتبر (از QueryString یا کوکی `nk_last_company`) موجود باشد، مستقیم به `ShopAdmin/{company}/Login` هدایت می‌شود.
  - اگر company در دسترس نباشد، کاربر به صفحه ثبت‌نام برمی‌گردد.
  - لینک‌های ورود مدیر کسب‌وکار در هدر/فوتر و صفحه Signup به مسیر مستقیم لاگین اختصاصی company آخر متصل شدند.
- فایل‌های کلیدی:
  - `Controllers/SignupController.cs`
  - `Views/Shared/_Layout.cshtml`
  - `Views/Signup/Index.cshtml`

## DONE-010 — بهینه‌سازی محتوای لاگین ShopAdmin برای ورود اولیه
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - حذف نمایش «کد شرکت» از بالای فرم لاگین ShopAdmin.
  - حذف متن «این صفحه مخصوص صاحب سایت است».
  - افزودن متن خلاصه درباره امکانات پنل (مدیریت، سفارش/رزرو، پیام‌ها، ظاهر سایت).
- فایل‌های کلیدی:
  - `Views/ShopAdmin/Login.cshtml`

## DONE-011 — BL-020 اصلاح پیام محصول برای اداره حضوری/آفلاین
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - Features هر SiteKind، صفحات ProductLandings، FAQ سایت و توضیحات کاتالوگ برای سناریوی بدون فروش آنلاین شفاف شد.
  - محصول `digital` در کاتالوگ به‌عنوان فروشگاه گجت فیزیکی از دوره/فایل آموزشی تفکیک شد.
  - callout در صفحه جزئیات محصول و migration یک‌باره `ApplyOfflineProductMessaging` اضافه شد.
- فایل‌های کلیدی:
  - `Helpers/SiteKinds.cs`, `Helpers/ProductLandings.cs`, `Helpers/PlatformModules.cs`
  - `Services/SiteContentStore.cs`, `Services/SiteStore.cs`, `Services/Data/NikooDb.cs`
  - `Views/Products/Details.cshtml`, `Views/Products/Index.cshtml`

## DONE-012 — BL-013 صندوق فروش سریع (POS) MVP
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - صفحه `ShopAdmin/Pos` با کاتالوگ قابل جستجو، سبد تعاملی، دریافت نقد/بانک و مشتری نقدی پیش‌فرض.
  - ثبت از مسیر `TenantStore.SaveSale` با کسر موجودی و سند حسابداری.
  - لینک در منوی حسابداری، فاکتورهای فروش و راهنمای حسابداری.
- فایل‌های کلیدی:
  - `Controllers/ShopAdminController.Accounting.cs`
  - `Views/ShopAdmin/Pos.cshtml`
  - `Views/Shared/_ShopAdminLayout.cshtml`
  - `Views/ShopAdmin/SaleInvoices.cshtml`, `Views/ShopAdmin/AccountingGuide.cshtml`

## DONE-013 — BL-015 حسابداری و تسویه نقدی نوبت‌دهی
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - ماژول حسابداری برای `SiteKinds.Booking` در منو و اکشن‌ها فعال شد.
  - دریافت مانده نوبت از صفحه جزئیات نوبت (نقد/بانک) با `RecordAppointmentPayment`.
  - بیعانه آنلاین نوبت در دفاتر با `JournalSource.Appointment` ثبت می‌شود.
- فایل‌های کلیدی:
  - `Controllers/ShopAdminController.Booking.cs`
  - `Controllers/ShopAdminController.Accounting.cs`
  - `Services/TenantStore.Accounting.cs`, `Services/TenantStore.Booking.cs`
  - `Views/ShopAdmin/AppointmentDetails.cshtml`
  - `Models/Store/AccountingModels.cs`

## DONE-014 — BL-016 پیامک تأیید و یادآوری نوبت
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - پیامک تأیید پس از قطعی شدن نوبت (پنل یا سایت/پس از بیعانه).
  - یادآوری خودکار قبل از نوبت با تایمر ساعتی اپ (`SendDueBookingReminders`).
  - تنظیمات `SmsBookingConfirm` و `SmsBookingReminderHours`؛ منوی پیامک برای Booking/Campus.
- فایل‌های کلیدی:
  - `Services/SmsService.cs`, `Services/TenantStore.Booking.cs`, `Global.asax.cs`
  - `Models/Store/StoreModels.cs`, `Models/Store/BookingModels.cs`
  - `Views/ShopAdmin/Settings.cshtml`, `Views/ShopAdmin/Sms.cshtml`
  - `Controllers/ShopAdminController.Booking.cs`, `Controllers/ShopController.Booking.cs`

## DONE-015 — BL-014 فروش و فعال‌سازی دستی دوره/فایل
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - صفحه `GrantAccess` در پنل Digital: انتخاب محصول دیجیتال، مشتری (یا ساخت حساب با رمز موقت)، مبلغ اختیاری، دریافت نقد/بانک.
  - `GrantDigitalAccess` سفارش `Completed` می‌سازد تا کتابخانه فعال شود؛ مبلغ > ۰ با `PostManualDigitalSale` در صندوق/بانک و فروش آنلاین (بدون حساب درگاه) سند می‌خورد.
  - منو، لینک از دانشجویان و سفارش‌ها، FAQ و راهنمای حسابداری به‌روز شد.
- فایل‌های کلیدی:
  - `Services/TenantStore.cs`, `Services/TenantStore.Accounting.cs`
  - `Controllers/ShopAdminController.Sales.cs`
  - `Views/ShopAdmin/GrantAccess.cshtml`, `Customers.cshtml`, `Orders.cshtml`
  - `Views/Shared/_ShopAdminLayout.cshtml`, `Views/ShopAdmin/AccountingGuide.cshtml`
  - `Helpers/SiteKinds.cs`, `Services/SiteContentStore.cs`

## DONE-016 — BL-017 کاربران و نقش‌های پنل
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - جدول `PanelUsers` با نقش‌های مدیر / صندوقدار / منشی کنار حساب مالک در Settings.
  - ورود یکپارچه؛ نشست نقش؛ `PanelAccess` برای گیت اکشن و فیلتر منو.
  - صفحه `PanelUsers` فقط برای مالک؛ اعتبار مالک در Settings فقط برای مالک قابل تغییر است.
- فایل‌های کلیدی:
  - `Models/Store/PanelUserModels.cs`, `Helpers/PanelAccess.cs`
  - `Services/TenantStore.PanelUsers.cs`, `Services/Data/NikooTables.cs`
  - `Controllers/ShopBaseController.cs`, `Controllers/ShopAdminController.cs`, `Controllers/ShopAdminController.PanelUsers.cs`
  - `Views/ShopAdmin/PanelUsers.cshtml`, `EditPanelUser.cshtml`, `Settings.cshtml`
  - `Views/Shared/_ShopAdminLayout.cshtml`, `Helpers/PlatformModules.cs`, `Helpers/SiteKinds.cs`

## DONE-017 — BL-021 بازنگری حسابداری SME
- وضعیت: تکمیل‌شده
- خلاصه پیاده‌سازی:
  - واژگان kind-aware با `AccountingCopy`؛ منوی Booking دوباره صندوق و فاکتور فروش/خدمت دارد.
  - ردیف پویا در فاکتور فروش، خرید و سند دستی (شروع ۳–۴ ردیف، افزودن/حذف تا ۲۴).
  - POS با ردیف دستی خدمت؛ پیام‌های عمومی به‌جای «کالا».
  - برای Booking/Campus ردیف بدون موجودی به حساب شهریه/خدمت (`6103`) می‌رود.
  - CSS مشترک حسابداری و راهنمای قدم‌به‌قدم بر اساس kind.
- فایل‌های کلیدی:
  - `Helpers/AccountingCopy.cs`, `Content/accounting.css`, `Content/accounting-lines.js`
  - `Views/ShopAdmin/EditSaleInvoice.cshtml`, `EditPurchase.cshtml`, `EditJournal.cshtml`, `Pos.cshtml`
  - `Views/ShopAdmin/AccountingGuide.cshtml`, `SaleInvoices.cshtml`, `Inventory.cshtml`, `Parties.cshtml`
  - `Services/TenantStore.Accounting.cs`, `Controllers/ShopAdminController.Accounting.cs`
  - `Views/Shared/_ShopAdminLayout.cshtml`
