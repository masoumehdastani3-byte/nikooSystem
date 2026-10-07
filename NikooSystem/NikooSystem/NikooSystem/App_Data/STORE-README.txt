# فروشگاه چندمستأجری نیکو سیستم آفرین

هر مشتری با یک «کد شرکت» دیتابیس/داده جدا دارد و می‌تواند دامنه اختصاصی بگیرد.

## دموها
- پوشاک: `/Shop/FASHION` یا `/Demo/fashion`
- مواد غذایی: `/Shop/GROCERY` یا `/Demo/grocery`
- دیجیتال: `/Shop/DIGITAL` یا `/Demo/digital`

## پنل ادمین فروشگاه
- `/ShopAdmin/{CompanyCode}/Login`
- کاربر پیش‌فرض: `admin`
- رمز: `Admin@123`

امکانات ادمین: اطلاعات فروشگاه، رنگ/چیدمان، محصولات، گروه‌های درختی، خصوصیات، منوی درختی، صفحات، گالری (thumb+large)، سفارش‌ها، مشتریان، تنظیم MerchantId درگاه.

## پنل مشتری
- ثبت‌نام/ورود: `/ShopAccount/{CompanyCode}/Register`
- سفارش‌ها و پروفایل + سبد خرید در فروشگاه عمومی

## داده هر مستأجر
`App_Data/Tenants/{CompanyCode}/`
- settings.json, products.json, categories.json, attributes.json
- menus.json, pages.json, gallery.json, customers.json, orders.json

رجیستری مستأجرها: `App_Data/tenants.json`  
فیلد `CustomDomain` را برای دامنه مشتری پر کنید (مثلاً `shop.mysite.ir`).

## تصاویر
آپلودها در `Uploads/{CompanyCode}/thumbs` و `Uploads/{CompanyCode}/large` ذخیره می‌شوند.
