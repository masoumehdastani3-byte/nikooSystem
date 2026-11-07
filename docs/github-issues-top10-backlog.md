# Top 10 GitHub Issues (Backlog Prioritized)

> Note: Direct GitHub issue creation is not available in this environment (GitHub CLI/API auth not configured). The following 10 issues are ready to copy into GitHub.

---

## 1) [Conversion] بازنویسی CTAها به پیام‌های نتیجه‌محور در صفحات کلیدی
**Description**
CTAهای فعلی در برخی صفحات عمومی هستند و خروجی اقدام را شفاف نمی‌کنند. باید CTAها با پیام‌های نتیجه‌محور و قابل سنجش جایگزین شوند.

**Acceptance Criteria**
- CTA اصلی صفحه اصلی به شکل نتیجه‌محور (مثلاً «شروع رایگان ۱۴ روزه») نمایش داده شود.
- CTAهای صفحات Products/Solutions/Signup/Checkout از نظر لحن و هدف یکپارچه شوند.
- نرخ کلیک CTA (CTR) نسبت به baseline قابل اندازه‌گیری شود.

**Technical Notes**
- Update view texts in:
  - `Views/Home/Index.cshtml`
  - `Views/Products/*.cshtml`
  - `Views/Solutions/*.cshtml`
  - `Views/Signup/Index.cshtml`
  - `Views/Checkout/*.cshtml`, `Views/Pay/*.cshtml`
- Keep copy strings centralized if possible.

**Estimated Effort**
- S (1-2 days)

**Dependencies**
- None

---

## 2) [UX] تفکیک واضح مسیرهای ورود بر اساس نقش کاربر (فروشنده/مشتری/مدیریت)
**Description**
ورودها برای کاربر جدید مبهم است. باید مسیر نقش‌ها در هدر و صفحات لاگین واضح شود.

**Acceptance Criteria**
- در هدر، مسیرهای ورود با برچسب روشن برای هر نقش نمایش داده شود.
- هر صفحه لاگین توضیح دهد «این صفحه مخصوص چه کسی است».
- کاهش خطای ورود اشتباه نقش در لاگ‌ها قابل مشاهده باشد.

**Technical Notes**
- `Views/Shared/_Layout.cshtml`
- `Views/Signup/Login.cshtml`
- `Views/ShopAccount/Login.cshtml`
- `Views/Admin/Login.cshtml`
- `Views/ShopAdmin/Login.cshtml`

**Estimated Effort**
- S (1 day)

**Dependencies**
- None

---

## 3) [Checkout] بهبود صفحه پرداخت ناموفق با مسیر نجات کامل (Retry + پشتیبانی)
**Description**
کاربر در پرداخت ناموفق مسیر روشن برای اقدام بعدی ندارد. باید صفحه Failed پیام وضعیت و مسیر نجات کامل داشته باشد.

**Acceptance Criteria**
- پیام واضح وضعیت تراکنش (موفق نشده/در حال بررسی) نمایش داده شود.
- دکمه Retry مستقیم برای ادامه پرداخت نمایش داده شود.
- لینک/CTA پشتیبانی سریع در همان صفحه وجود داشته باشد.

**Technical Notes**
- `Views/Checkout/Failed.cshtml`
- `Controllers/CheckoutController.cs` (retry target)
- `Views/Pay/Start.cshtml` (resume flow)

**Estimated Effort**
- M (2-3 days)

**Dependencies**
- Issue #4 (اختیاری برای هم‌راستایی پیام‌ها)

---

## 4) [Conversion] یکپارچه‌سازی پیام Trial→Paid در نقاط اصلی قیف
**Description**
پیام‌های تبدیل آزمایشی به پولی در صفحات مختلف ناهماهنگ‌اند. باید پیام واحد با ارزش و فوریت مشخص تعریف شود.

**Acceptance Criteria**
- پیام Trial و مسیر ارتقا در Signup/Blog/Tools یکپارچه شود.
- CTA ارتقا در هر صفحه دارای یک هدف واضح باشد.
- افت در مرحله Trial→Paid بعد از انتشار قابل سنجش شود.

**Technical Notes**
- `Views/Signup/Index.cshtml`
- `Views/Blog/Post.cshtml`
- `Views/Tools/_ToolCta.cshtml`

**Estimated Effort**
- S-M (1-2 days)

**Dependencies**
- None

---

## 5) [UX] استانداردسازی Empty State در لیست‌ها و داشبوردها
**Description**
در برخی صفحات خالی، کاربر دلیل خالی بودن و قدم بعدی را نمی‌داند.

**Acceptance Criteria**
- هر Empty State شامل: علت خالی بودن + یک CTA اصلی + راهنمای قدم اول باشد.
- الگوی Empty State در پنل ادمین/فروشنده/مشتری یکپارچه باشد.

**Technical Notes**
- `Views/ShopAdmin/*.cshtml` (list pages)
- `Views/ShopAccount/Orders.cshtml`
- `Views/Admin/*.cshtml` (empty tables/cards)

**Estimated Effort**
- M (2-4 days)

**Dependencies**
- None

---

## 6) [Analytics] پیاده‌سازی Event Tracking کامل قیف (GA4/GTM + Server Events)
**Description**
برای بهینه‌سازی نرخ تبدیل باید همه نقاط قیف قابل اندازه‌گیری شوند.

**Acceptance Criteria**
- رویدادها ثبت شوند: `view_pricing`, `start_signup`, `complete_signup`, `start_checkout`, `purchase_success`, `purchase_failed`.
- رویدادهای حساس پرداخت در سرور نیز log/event شوند.
- مستندات نام‌گذاری eventها در docs ثبت شود.

**Technical Notes**
- `Views/Shared/_SeoHead.cshtml` (script container/hooks)
- `Views/Signup/*.cshtml`
- `Views/Checkout/*.cshtml`
- `Views/Pay/*.cshtml`
- `Controllers/CheckoutController.cs`, `Controllers/PayController.cs`

**Estimated Effort**
- M (3-5 days)

**Dependencies**
- None

---

## 7) [SEO] یکپارچه‌سازی Canonical و Host برای جلوگیری از پراکندگی سیگنال
**Description**
Canonical/host ناسازگار می‌تواند باعث افت رتبه و دوگانگی URL شود.

**Acceptance Criteria**
- canonical همه صفحات اصلی به host استاندارد اشاره کند.
- ریدایرکت hostهای ناهمسان به host اصلی فعال باشد.
- بررسی نمونه URLها در محیط production مستند شود.

**Technical Notes**
- `Helpers/Seo.cs`
- `Controllers/SeoController.cs`
- `Views/Admin/SiteSettings.cshtml` (config guidance)

**Estimated Effort**
- M (2-3 days)

**Dependencies**
- None

---

## 8) [SEO/Conversion] لینک‌سازی داخلی هدفمند از بلاگ به صفحات پول‌ساز
**Description**
بلاگ باید به‌صورت سیستماتیک کاربران را به Signup/Products/Solutions هدایت کند.

**Acceptance Criteria**
- هر پست بلاگ حداقل 2 لینک داخلی هدفمند به صفحات تبدیل‌محور داشته باشد.
- بلوک CTA پایان مقاله یکسان و قابل پیگیری باشد.
- افت صفحات یتیم (orphan-like) کاهش یابد.

**Technical Notes**
- `Views/Blog/Post.cshtml`
- `Views/Blog/Index.cshtml`
- `Services/SiteBlogStore.cs` (if metadata needed)

**Estimated Effort**
- S-M (1-3 days)

**Dependencies**
- Issue #1 (هماهنگی CTA)

---

## 9) [SEO] ارتقای Sitemap با `lastmod` واقعی و ساختار بهینه
**Description**
sitemap فعلی نیاز به سیگنال freshness بهتر دارد.

**Acceptance Criteria**
- `lastmod` برای URLهای اصلی/محتوایی بر اساس داده واقعی تولید شود.
- ساختار sitemap برای رشد URLها پایدار باشد.
- صحت خروجی sitemap با نمونه URLها تست شود.

**Technical Notes**
- `Controllers/SeoController.cs`
- `Services/SiteBlogStore.cs`
- related store data providers

**Estimated Effort**
- M (2-4 days)

**Dependencies**
- Issue #7 (optional alignment)

---

## 10) [Usability] یکپارچه‌سازی واژگان پنل‌ها (Admin/ShopAdmin/ShopAccount)
**Description**
نام‌گذاری‌های تخصصی/ناهمسان بار شناختی ایجاد می‌کنند.

**Acceptance Criteria**
- واژگان کلیدی UI در پنل‌ها یکدست شوند.
- اصطلاحات تخصصی با متن ساده یا tooltip کوتاه همراه شوند.
- راهنمای واژگان در docs ثبت شود.

**Technical Notes**
- `Views/Shared/_AdminLayout.cshtml`
- `Views/Shared/_ShopAdminLayout.cshtml`
- `Views/ShopAccount/Profile.cshtml`
- key dashboard/list pages

**Estimated Effort**
- M (2-3 days)

**Dependencies**
- None
