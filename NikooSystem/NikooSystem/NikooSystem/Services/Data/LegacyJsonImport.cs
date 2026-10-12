using System.IO;
using System.Web.Hosting;
using Newtonsoft.Json;

namespace NikooSystem.Services.Data
{
    /// <summary>
    /// Copies data from the JSON files older versions kept (App_Data/*.json, App_Data/Tenants/*, Content/tutorials/*.json)
    /// into the database. Each import runs once (recorded in SchemaInfo) and does nothing when the files are gone.
    /// </summary>
    internal static class LegacyJsonImport
    {
        public static void Run()
        {
            var appData = HostingEnvironment.MapPath("~/App_Data");
            if (string.IsNullOrEmpty(appData) || !Directory.Exists(appData)) return;

            TenantStore.ImportLegacyJson(appData);
            SiteStore.ImportLegacyJson(appData);
        }

        public static void RunTutorials()
        {
            TutorialService.ImportLegacyFiles();
        }

        /// <summary>The file's contents, or null when there is no such file.</summary>
        public static T Read<T>(string path) where T : class
        {
            return File.Exists(path) ? JsonConvert.DeserializeObject<T>(File.ReadAllText(path)) : null;
        }
    }
}
