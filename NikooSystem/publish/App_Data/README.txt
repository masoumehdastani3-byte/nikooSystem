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
فایل `App_Data/payment-clients.json`:

```json
{
  "AppId": "my-shop",
  "ApiKey": "your-secret-key",
  "Name": "فروشگاه من",
  "AllowedReturnHosts": [ "myshop.ir", "localhost" ]
}
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

## امکانات
- صفحه اصلی با ظاهر مشابه قالب استاتیک
- لیست و جزئیات محصولات نرم‌افزاری
- دمو فروشگاه‌های آنلاین (پوشاک / مواد غذایی / دیجیتال)
- فرم درخواست دمو + پرداخت آنلاین
- هاب پرداخت قابل‌استفاده در پروژه‌های دیگر
- پنل ادمین: محصولات، سفارشات، داشبورد

داده محصولات، سفارشات و پرداخت‌های هاب در `App_Data/*.json` ذخیره می‌شود.
