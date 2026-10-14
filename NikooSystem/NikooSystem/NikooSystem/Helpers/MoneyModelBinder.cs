using System.Globalization;
using System.Text;
using System.Web.Mvc;

namespace NikooSystem.Helpers
{
    /// <summary>
    /// Binds long and long? (every amount in the site is a long) from text that may hold thousands separators or
    /// Persian/Arabic digits, e.g. «۱۵۰٬۰۰۰» or "150,000", for when money-input.js did not run before the form was sent.
    /// </summary>
    public class MoneyModelBinder : IModelBinder
    {
        public static void Register(ModelBinderDictionary binders)
        {
            binders[typeof(long)] = new MoneyModelBinder();
            binders[typeof(long?)] = new MoneyModelBinder();
        }

        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (result == null) return null;
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);

            var text = Clean(result.AttemptedValue);
            if (text.Length == 0) return null;

            long value;
            if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)) return value;

            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "عدد واردشده معتبر نیست.");
            return null;
        }

        private static string Clean(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw.Trim())
            {
                if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
                else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
                else if (ch == ',' || ch == '٬' || ch == '،' || ch == ' ' || ch == '\u00A0' || ch == '\u200C') continue;
                else if (ch == '−') sb.Append('-');
                else sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
