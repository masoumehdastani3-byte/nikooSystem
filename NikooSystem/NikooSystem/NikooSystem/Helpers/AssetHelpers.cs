using System.IO;
using System.Web.Hosting;
using System.Web.Mvc;

namespace NikooSystem.Helpers
{
    public static class AssetHelpers
    {
        /// <summary>
        /// URL of a static file with its modification time appended (<c>site.css?v=…</c>), so browsers fetch the new copy
        /// after a deploy instead of reusing a cached one.
        /// </summary>
        public static string Asset(this UrlHelper url, string path)
        {
            var href = url.Content(path);
            var file = HostingEnvironment.MapPath(path);
            return file != null && File.Exists(file) ? href + "?v=" + File.GetLastWriteTimeUtc(file).Ticks.ToString("x") : href;
        }
    }
}
