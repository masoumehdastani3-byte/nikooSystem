# نیکو سیستم آفرین — فروشگاه محصولات نرم‌افزاری

ASP.NET MVC 5 (.NET Framework 4.8)

## اجرا
1. پروژه `NikooSystem.slnx` را در Visual Studio باز کنید
2. پروژه `NikooSystem` را Run کنید (IIS Express)

## پنل مدیریت
- آدرس: `/Admin/Login`
- کاربر: `admin`
- رمز: `Admin@123`
(در `Web.config` قابل تغییر است)

## درگاه پرداخت
در `Web.config`:
- `ZarinPal.MerchantId` — مرچنت کد پنل
- `ZarinPal.Sandbox` — برای تست `true`
- `ZarinPal.CallbackUrl` — بازگشت فروشگاه داخلی: `/Checkout/Callback`
- `PaymentHub.BaseUrl` — مثلاً `https://nikoooo.ir`
- `PaymentHub.CallbackUrl` — بازگشت هاب: `https://nikoooo.ir/Pay/Callback`

## هاب پرداخت (برای پروژه‌های دیگر)

سایر سایت‌ها بدون اتصال مستقیم به درگاه، از nikoooo.ir برای پرداخت استفاده می‌کنند.

راهنمای کامل در سایت: `/Pay/Guide`

### جریان کلی
1. پروژه شما با API یک پرداخت می‌سازد و `paymentUrl` می‌گیرد
2. کاربر به صفحه پرداخت nikoooo.ir می‌رود (`/Pay/Start/{token}`)
3. بعد از درگاه، nikoooo.ir وضعیت را تأیید می‌کند (`/Pay/Callback`)
4. کاربر با پارامترهای امضاشده به `returnUrl` پروژه شما برمی‌گردد
5. اختیاری: وب‌هوک `notifyUrl` هم صدا زده می‌شود

### ۱) ثبت کلاینت
یک ردیف در جدول `PaymentClients` دیتابیس `nikoo` و دامنه‌های مجاز بازگشت در `PaymentClientHosts`:

```sql
INSERT INTO PaymentClients (AppId, ApiKey, Name) VALUES (N'my-shop', N'your-secret-key', N'فروشگاه من');
INSERT INTO PaymentClientHosts (AppId, Position, Host) VALUES
  (N'my-shop', 0, N'myshop.ir'),
  (N'my-shop', 1, N'localhost');
```

### ۲) ساخت پرداخت
`POST https://nikoooo.ir/api/payments/create`

هدرها:
- `X-App-Id: my-shop`
- `X-Api-Key: your-secret-key`
- `Content-Type: application/json`

بدنه:
```json
{
  "amount": 150000,
  "description": "سفارش ۱۲۳",
  "returnUrl": "https://myshop.ir/payment/return",
  "notifyUrl": "https://myshop.ir/payment/notify",
  "externalRef": "ORD-123",
  "customerName": "علی رضایی",
  "customerMobile": "09121234567",
  "customerEmail": "ali@example.com"
}
```

پاسخ:
```json
{
  "success": true,
  "token": "...",
  "paymentUrl": "https://nikoooo.ir/Pay/Start/...",
  "amount": 150000
}
```

کاربر را به `paymentUrl` بفرستید.

### ۳) بازگشت کاربر (ReturnUrl)
بعد از پرداخت، کاربر با queryهای امضاشده برمی‌گردد:
`token`, `status` (`paid`|`failed`), `amount`, `externalRef`, `refId`, `timestamp`, `signature`

امضا: HMAC-SHA256 روی  
`token|status|amount|externalRef|refId|timestamp`  
با همان `ApiKey`.

### ۴) وب‌هوک (NotifyUrl)
همان فیلدها به‌صورت JSON با `POST` ارسال می‌شود.

### ۵) استعلام وضعیت
`GET https://nikoooo.ir/api/payments/status/{token}`  
با همان هدرهای `X-App-Id` و `X-Api-Key`.

## اتصال ترب (Torob Product API v3)
هر فروشگاه tenant در پنل ShopAdmin → تنظیمات می‌تواند API ترب را فعال کند.

- Endpoint (با دامنه اختصاصی): `POST https://دامنه-فروشگاه/torob_api/v3/products`
- بدون دامنه: `POST https://سایت-اصلی/torob_api/v3/{CompanyCode}/products`
- احراز هویت: هدر `X-Torob-Token` (JWT امضای ترب) و `X-Torob-Token-Version: 1`
- بدنه نمونه: `{"page":1,"sort":"date_added_desc"}`
- Webhook به‌روزرسانی (اختیاری): توکن Bearer از ترب در تنظیمات فروشگاه؛ پس از ذخیره محصول به `api.torob.com` اطلاع داده می‌شود.
- تست محلی: در Web.config می‌توانید `Torob.SkipJwtValidation` را `true` کنید (production: false).

## امکانات
- صفحه اصلی با ظاهر مشابه قالب استاتیک
- لیست و جزئیات محصولات نرم‌افزاری
- دمو فروشگاه‌های آنلاین (پوشاک / مواد غذایی / دیجیتال)
- فرم درخواست دمو + پرداخت آنلاین
- هاب پرداخت قابل‌استفاده در پروژه‌های دیگر
- پنل ادمین: محصولات، سفارشات، داشبورد

## دیتابیس
همه داده‌ها (فروشگاه‌ها، تنظیمات و چیدمان صفحات، محصولات، گروه‌ها، منوها، صفحات، گالری، مشتریان، سفارش‌ها،
نظرات، سبد خرید، محصولات و سفارش‌های سایت اصلی، کلاینت‌ها و پرداخت‌های هاب، آموزش‌های پنل) در دیتابیس
SQL Server به نام `nikoo` ذخیره می‌شود. هیچ داده‌ای در فایل JSON نگه‌داری نمی‌شود.

- رشته اتصال: `connectionStrings` → `Nikoo` در `Web.config` (پیش‌فرض: `(localdb)\MSSQLLocalDB`)
- دیتابیس و جدول‌ها در اولین اجرا خودکار ساخته می‌شوند (کاربر اتصال باید اجازه `CREATE DATABASE` داشته باشد،
  یا دیتابیس خالی `nikoo` را از قبل بسازید). ساختار جدول‌ها در `Services/Data/NikooTables.cs` تعریف شده است.
- هر فیلد مدل یک ستون جداست و هر فهرست یک جدول فرزند با ستون `Position` (ترتیب) و کلید خارجی به جدول اصلی
  (حذف آبشاری). مثلاً: `ProductImages`، `ProductAttributes`، `ProductCollections`، `OrderItems`، `CartItems`،
  `StoreHomeSections`، `PaymentClientHosts`، `AttributeDefCategories`، `TutorialSteps`.
- اگر به مدلی فیلد تازه اضافه شود، ستونش در اجرای بعدی خودکار اضافه می‌شود.
- سبد خرید: جدول‌های `Carts` و `CartItems`؛ شناسه سبد در کوکی `nikoo_cart` است و سبدهای قدیمی‌تر از ۳۰ روز پاک می‌شوند.
- آموزش‌های پنل: جدول‌های `Tutorials`، `TutorialViews`، `TutorialSteps` و `TutorialVideos`. فایل
  `App_Data/Seed/tutorials.sql` (ساخته‌شده با `tools/tutorials/export-tutorials.ps1`) هر بار که محتوایش عوض شود
  در شروع سایت اجرا می‌شود؛ خود ویدئوها در `Content/tutorials/videos` روی دیسک هستند.
- اگر روی سروری هنوز فایل‌های نسخه‌های قبلی (`App_Data/*.json`، `App_Data/Tenants/*`، `Content/tutorials/*.json`)
  باشد، در اولین اجرا یک‌بار به دیتابیس منتقل می‌شوند و بعد از آن می‌توان آن‌ها را حذف کرد.
- تصاویر همچنان در پوشه `Uploads` روی دیسک هستند و فقط آدرسشان در دیتابیس ذخیره می‌شود.
