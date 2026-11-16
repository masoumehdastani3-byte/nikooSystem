using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Filters;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Public page for the books that ship with shop, course, booking, campus and repair sites (/accounting).</summary>
    [CountVisits]
    public class AccountingController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Nav = "accounting";
            var site = SiteContentStore.Settings;
            var faqs = Faqs().ToList();
            ViewBag.Faqs = faqs;
            ViewBag.Products = ProductLandings.All.Where(HasBooks).ToList();

            var seo = Seo.Of(HttpContext);
            seo.Title = "حسابداری دوطرفه فروشگاه، آموزش و نوبت‌دهی" + SeoPages.Separator + site.SiteName;
            seo.Description = "حسابداری دوطرفه با گروه، کل، معین و تفصیلی، صندوق فروش، فاکتور خرید و فروش، بانک، انبار، تراز چهارستونی و سال مالی شمسی؛ داخل پنل، همراه فروش آنلاین و حضوری.";
            seo.Canonical = seo.Url(Url.RouteUrl("Accounting"));
            seo.Schemas.Add(SeoPages.Faq(faqs));
            seo.Schemas.Add(SeoPages.Breadcrumbs(seo,
                SeoPages.Crumb("خانه", "/"),
                SeoPages.Crumb("محصولات", Url.Action("Index", "Products")),
                SeoPages.Crumb("حسابداری", null)));
            return View();
        }

        static bool HasBooks(ProductLanding page) =>
            page.Kind == SiteKinds.Shop || page.Kind == SiteKinds.Digital || page.Kind == SiteKinds.Booking
            || page.Kind == SiteKinds.Campus || page.Kind == SiteKinds.Repair;

        static IEnumerable<SiteFaq> Faqs()
        {
            yield return new SiteFaq
            {
                Question = "حسابداری همراه کدام محصولات است؟",
                Answer = "فروشگاه کالای فیزیکی، فروش دوره و فایل، نوبت‌دهی (مطب، آرایشگاه و مشاوره)، آموزشگاه، باشگاه ورزشی و تعمیرگاه. "
                    + "سرفصل، سند، صندوق، فاکتور، بانک، انبار و تراز داخل همان پنل است. "
                    + "سایت شرکتی، سایت خبری و منوی کافه برای معرفی و محتوا ساخته شده‌اند و دفتر حسابداری جدا ندارند."
            };
            yield return new SiteFaq
            {
                Question = "اگر فقط حضوری بفروشم هم دفتر کار می‌کند؟",
                Answer = "بله. سبد خرید و درگاه را لازم نیست روشن کنید. فروش حضوری را از صندوق یا فاکتور ثبت می‌کنید، دریافت نقد یا بانک می‌زنید و سند همان لحظه در دفاتر می‌نشیند. "
                    + "در فروش دوره، دسترسی دانشجو را از پنل می‌دهید و مبلغ در صندوق یا بانک ثبت می‌شود. "
                    + "در نوبت‌دهی، آموزشگاه، باشگاه و تعمیرگاه هم دریافت نقدی و فاکتور بدون فروش آنلاین ثبت می‌شود."
            };
            yield return new SiteFaq
            {
                Question = "سرفصل‌ها گروه، کل، معین و تفصیلی دارند؟",
                Answer = "بله. سرفصل سه سطحی است: گروه با کد یک‌رقمی، حساب کل با کد دورقمی و حساب معین با کد چهاررقمی. سند به معین می‌خورد و مانده کل و گروه خودکار جمع می‌شود. "
                    + "روی هر معین دو سطح تفصیلی شناور هست: سطح اول برای اشخاص (هر تامین‌کننده و مشتری تفصیلی خودش را دارد) و سطح دوم برای مرکز هزینه یا پروژه، مثل سالن یا شعبه. "
                    + "برای هر معین مشخص می‌کنید تفصیلی اجباری، اختیاری یا بدون تفصیلی باشد."
            };
            yield return new SiteFaq
            {
                Question = "سال مالی شمسی چطور بسته می‌شود؟",
                Answer = "در صفحه سال مالی، ماه شروع (معمولاً فروردین) را می‌گذارید. فهرست اسناد، فاکتورها، دریافت و پرداخت و تراز به‌طور پیش‌فرض روی همان سال فیلتر می‌شوند. "
                    + "با بستن سال تمام‌شده، مانده درآمد و هزینه به «سود و زیان انباشته» منتقل می‌شود. اگر قفل تاریخ روشن باشد، با تاریخ آن سال سند تازه ثبت نمی‌شود و در صورت نیاز همان سال را دوباره باز می‌کنید."
            };
            yield return new SiteFaq
            {
                Question = "روی فاکتور تخفیف و مالیات ارزش افزوده می‌شود زد؟",
                Answer = "بله. در فاکتور فروش، صندوق و فاکتور خرید مبلغ تخفیف و درصد مالیات بر ارزش افزوده را وارد می‌کنید و جمع قابل دریافت یا پرداخت همان لحظه حساب می‌شود. "
                    + "نرخ پیش‌فرض مالیات از صفحه سال مالی روشن و تنظیم می‌شود و مبلغ مالیات در سرفصل «مالیات و عوارض ارزش افزوده» سند می‌خورد. "
                    + "برگه چاپی فاکتور و سند، با نام کسب‌وکار، طرف‌حساب، ردیف‌ها و جمع آماده است."
            };
            yield return new SiteFaq
            {
                Question = "فروش آنلاین، شهریه و نوبت خودشان سند می‌خورند؟",
                Answer = "بله. سفارش آنلاین پرداخت‌شده، شهریه کلاس، رزرو سالن، بیعانه و تسویه نوبت، و صورتحساب تعمیر (اجرت و قطعه) سند خودکار می‌خورند. "
                    + "در فروشگاه، درآمد ارسال جدا از فروش کالا ثبت می‌شود و با تأیید مرجوعی، موجودی برمی‌گردد و سند برگشت از فروش زده می‌شود. "
                    + "فروش آنلاین در دفتر همان بانکی می‌نشیند که درگاه سایت به آن وصل است."
            };
        }
    }
}
