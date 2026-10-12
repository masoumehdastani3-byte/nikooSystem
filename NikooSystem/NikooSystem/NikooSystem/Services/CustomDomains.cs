using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using NikooSystem.Models.Store;

namespace NikooSystem.Services
{
    /// <summary>What one check of a store's domain found.</summary>
    public class DomainCheckResult
    {
        public string Domain { get; set; }
        public DateTime CheckedAt { get; set; }
        public List<string> RootIps { get; set; } = new List<string>();
        public List<string> RootIpv6 { get; set; } = new List<string>();
        public List<string> WwwIps { get; set; } = new List<string>();
        /// <summary>The domain itself reaches this site (by IP, or through a CDN that answered the probe).</summary>
        public bool RootOk { get; set; }
        public bool WwwOk { get; set; }
        /// <summary>The site answered on the domain behind IPs that aren't the server's (a CDN such as ArvanCloud or Cloudflare).</summary>
        public bool ViaProxy { get; set; }
        /// <summary>What the owner still has to fix, in Persian.</summary>
        public List<string> Problems { get; set; } = new List<string>();
    }

    /// <summary>
    /// Connecting a store's own domain. The owner enters it and points its DNS at the server (the panel's «دامنه اختصاصی»
    /// guide); the hourly job and the «بررسی دوباره» button check the DNS. Once it is right, Nikoo's support is told in Bale to
    /// add the IIS binding and the SSL certificate and marks the domain active on «دامنه‌های مشتریان»; only then is the
    /// domain used as the store's public address.
    /// </summary>
    public static class CustomDomains
    {
        /// <summary>Answered on every connected domain (Global.asax) so the check also works behind a CDN.</summary>
        public const string ProbePath = "/nikoo-domain-check";

        /// <summary>How long after entering the domain the owner gets one reminder that its DNS still points elsewhere.</summary>
        public static readonly TimeSpan ReminderAfter = TimeSpan.FromHours(24);

        private static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(5);
        private const int ProbeTimeoutMs = 5000;

        public static string ProbeAnswer(string companyCode) => "nikoo-domain:" + companyCode.ToUpperInvariant();

        /// <summary>The address of the domain's records for www: the store's platform subdomain when there is one, else the IP.</summary>
        public static string WwwTarget(string companyCode)
        {
            var root = TenantProvisioningService.RootDomain;
            return string.IsNullOrEmpty(root) ? TenantProvisioningService.ServerIp : companyCode.ToLowerInvariant() + "." + root;
        }

        public static DomainCheckResult LastCheck(TenantInfo t)
        {
            if (string.IsNullOrWhiteSpace(t?.DomainCheck)) return null;
            try
            {
                var check = JsonConvert.DeserializeObject<DomainCheckResult>(t.DomainCheck);
                return check != null && string.Equals(check.Domain, t.CustomDomain, StringComparison.OrdinalIgnoreCase) ? check : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Saves a new domain, or removes it with an empty input, and starts its connection over (checking the new one right
        /// away). Support is told when a domain it had connected or was about to connect is let go. Returns the error, or null.
        /// </summary>
        public static string Change(TenantInfo t, string input)
        {
            var domain = TenantProvisioningService.NormalizeDomain(input);
            if (string.Equals(domain, (t.CustomDomain ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return null;
            var error = TenantProvisioningService.ValidateDomain(t.CompanyCode, domain);
            if (error != null) return error;

            var oldDomain = (t.CustomDomain ?? "").Trim();
            var oldState = t.DomainStatus();
            t.CustomDomain = domain;
            t.DomainSetAt = domain.Length == 0 ? (DateTime?)null : DateTime.Now;
            t.DomainDnsOkAt = null;
            t.DomainActivatedAt = null;
            t.DomainCheck = null;
            t.DomainRemindedAt = null;
            TenantStore.UpdateTenant(t);

            if (oldState == DomainState.WaitingSupport || oldState == DomainState.Active)
                Notifications.DomainReleased(t, oldDomain, oldState == DomainState.Active);
            if (domain.Length > 0) Check(t);
            return null;
        }

        /// <summary>Checks the domain's DNS now and saves the result; the first time it is right, support and the owner are told.</summary>
        public static DomainCheckResult Check(TenantInfo t)
        {
            var domain = (t?.CustomDomain ?? "").Trim();
            if (domain.Length == 0) return null;

            var expected = TenantProvisioningService.ServerIps;
            var result = new DomainCheckResult { Domain = domain, CheckedAt = DateTime.Now };
            var root = Resolve(domain);
            var www = Resolve("www." + domain);
            result.RootIps = V4(root);
            result.RootIpv6 = root.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6).Select(a => a.ToString()).ToList();
            result.WwwIps = V4(www);

            result.RootOk = PointsHere(result.RootIps, expected);
            result.WwwOk = PointsHere(result.WwwIps, expected);
            if (!result.RootOk && result.RootIps.Count > 0 && !result.RootIps.Any(expected.Contains) && Probe(domain, root, t.CompanyCode))
                result.RootOk = result.ViaProxy = true;
            if (!result.WwwOk && result.WwwIps.Count > 0 && !result.WwwIps.Any(expected.Contains) && Probe("www." + domain, www, t.CompanyCode))
                result.WwwOk = true;
            result.Problems = Problems(result, expected);

            var firstOk = result.RootOk && !t.DomainDnsOkAt.HasValue && !t.DomainActivatedAt.HasValue;
            if (firstOk) t.DomainDnsOkAt = DateTime.Now;
            t.DomainCheck = JsonConvert.SerializeObject(result);
            TenantStore.SaveDomainCheck(t);

            if (firstOk)
            {
                Notifications.DomainNeedsSupport(t, result);
                Notifications.DomainDnsOk(t);
            }
            return result;
        }

        /// <summary>Support has added the binding and the certificate: the domain becomes the store's address.</summary>
        public static void Activate(TenantInfo t)
        {
            if (string.IsNullOrWhiteSpace(t.CustomDomain) || t.DomainActivatedAt.HasValue) return;
            t.DomainDnsOkAt = t.DomainDnsOkAt ?? DateTime.Now;
            t.DomainActivatedAt = DateTime.Now;
            TenantStore.UpdateTenant(t);
            Notifications.DomainActivated(t);
        }

        /// <summary>Hourly: checks the domains still waiting for their DNS and reminds owners once when it is still wrong a day later.</summary>
        public static void CheckPending()
        {
            var waiting = TenantStore.GetTenants()
                .Where(t => TenantStore.IsLive(t) && t.DomainStatus() == DomainState.WaitingDns && !TenantStore.IsDemo(t.CompanyCode))
                .ToList();
            foreach (var t in waiting)
            {
                try
                {
                    var result = Check(t);
                    if (result != null && !result.RootOk && !t.DomainRemindedAt.HasValue
                        && t.DomainSetAt.HasValue && DateTime.Now - t.DomainSetAt.Value >= ReminderAfter)
                    {
                        t.DomainRemindedAt = DateTime.Now;
                        TenantStore.SaveDomainCheck(t);
                        Notifications.DomainDnsReminder(t, result);
                    }
                }
                catch
                {
                    // checked again in the next run
                }
            }
        }

        public static List<TenantInfo> WithDomains()
        {
            return TenantStore.GetTenants()
                .Where(t => t.DomainStatus() != DomainState.None)
                .OrderBy(t => t.DomainStatus() == DomainState.WaitingSupport ? 0 : t.DomainStatus() == DomainState.WaitingDns ? 1 : 2)
                .ThenByDescending(t => t.DomainSetAt ?? t.CreatedAt)
                .ToList();
        }

        public static int CountWaitingSupport() => TenantStore.GetTenants().Count(t => t.DomainStatus() == DomainState.WaitingSupport);

        /// <summary>Domains entered before this guide existed were used right away; they stay the stores' addresses.</summary>
        internal static void MigrateExisting()
        {
            foreach (var t in TenantStore.GetTenants().Where(t => !string.IsNullOrWhiteSpace(t.CustomDomain) && !t.DomainSetAt.HasValue))
            {
                t.DomainSetAt = t.CreatedAt;
                t.DomainDnsOkAt = DateTime.Now;
                t.DomainActivatedAt = DateTime.Now;
                TenantStore.UpdateTenant(t);
            }
        }

        private static bool PointsHere(List<string> ips, List<string> expected)
        {
            return ips.Count > 0 && expected.Count > 0 && ips.All(expected.Contains);
        }

        private static List<string> Problems(DomainCheckResult r, List<string> expected)
        {
            var list = new List<string>();
            var server = expected.Count == 0 ? null : string.Join("، ", expected);
            if (server == null)
                list.Add("IP سرور هنوز در پنل نیکو ثبت نشده است؛ پشتیبانی آن را ثبت می‌کند. تا آن زمان می‌توانید IP را از پشتیبانی بپرسید.");

            if (!r.RootOk)
            {
                var ours = r.RootIps.Where(expected.Contains).ToList();
                if (r.RootIps.Count == 0)
                    list.Add("برای خود دامنه (" + r.Domain + ") هنوز هیچ رکورد A پیدا نشد؛ رکورد A با نام @ بسازید.");
                else if (ours.Count > 0)
                    list.Add("علاوه بر IP نیکو، رکورد A دیگری (" + string.Join("، ", r.RootIps.Except(ours)) + ") هم برای دامنه هست؛ آن را حذف کنید تا همه بازدیدکنندگان به سایت شما برسند.");
                else if (server != null)
                    list.Add("دامنه هنوز به " + string.Join("، ", r.RootIps) + " اشاره می‌کند، نه به IP نیکو (" + server + "). رکورد A با نام @ را روی IP نیکو بگذارید؛ اگر تازه عوض کرده‌اید، چند ساعت صبر کنید.");
                if (r.RootIpv6.Count > 0)
                    list.Add("دامنه رکورد AAAA (آدرس IPv6) دارد؛ آن را حذف کنید، چون سرور نیکو با IPv6 کار نمی‌کند و بخشی از بازدیدکنندگان به سایت نمی‌رسند.");
            }

            if (!r.WwwOk)
            {
                if (r.WwwIps.Count == 0)
                    list.Add("برای www هنوز رکوردی نیست؛ بدون آن، آدرس www." + r.Domain + " باز نمی‌شود.");
                else if (server != null)
                    list.Add("www." + r.Domain + " به " + string.Join("، ", r.WwwIps) + " اشاره می‌کند، نه به سرور نیکو؛ رکورد www را طبق جدول اصلاح کنید.");
            }
            return list;
        }

        private static IPAddress[] Resolve(string host)
        {
            try
            {
                var lookup = Dns.GetHostAddressesAsync(host);
                return lookup.Wait(DnsTimeout) ? lookup.Result : new IPAddress[0];
            }
            catch (AggregateException)
            {
                return new IPAddress[0];
            }
            catch (SocketException)
            {
                return new IPAddress[0];
            }
            catch (ArgumentException)
            {
                return new IPAddress[0];
            }
        }

        private static List<string> V4(IEnumerable<IPAddress> addresses) =>
            addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).Select(a => a.ToString()).Distinct().ToList();

        /// <summary>Asks the site on the domain for its store code; only public addresses are contacted.</summary>
        private static bool Probe(string host, IPAddress[] addresses, string companyCode)
        {
            if (addresses.Length == 0 || addresses.Any(a => !IsPublic(a))) return false;
            foreach (var scheme in new[] { "http", "https" })
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(scheme + "://" + host + ProbePath);
                    request.Timeout = ProbeTimeoutMs;
                    request.ReadWriteTimeout = ProbeTimeoutMs;
                    request.AllowAutoRedirect = false;
                    request.UserAgent = "NikooDomainCheck/1.0";
                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var buffer = new char[64];
                        var read = reader.ReadBlock(buffer, 0, buffer.Length);
                        if (new string(buffer, 0, read).Trim() == ProbeAnswer(companyCode)) return true;
                    }
                }
                catch (WebException) { }
                catch (IOException) { }
            }
            return false;
        }

        private static bool IsPublic(IPAddress address)
        {
            if (IPAddress.IsLoopback(address)) return false;
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.GetAddressBytes()[0] >= 0xfc && address.GetAddressBytes()[0] <= 0xfd);
            var b = address.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224
                     || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                     || (b[0] == 169 && b[1] == 254)
                     || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                     || (b[0] == 192 && b[1] == 168));
        }
    }
}
