using System;
using System.Collections.Generic;

namespace NikooSystem.Models
{
    /// <summary>The visits of one site on one day. CompanyCode is empty for Nikoo's own site.</summary>
    public class SiteVisitDay
    {
        public string CompanyCode { get; set; }
        public DateTime Day { get; set; }
        /// <summary>Pages viewed.</summary>
        public int Views { get; set; }
        /// <summary>Different people who came that day.</summary>
        public int Visitors { get; set; }
        /// <summary>Of <see cref="Visitors"/>, those who had not come before in the same Persian month.</summary>
        public int MonthVisitors { get; set; }
    }

    /// <summary>One IP address that visited a site on a day.</summary>
    public class SiteVisitIp
    {
        public string CompanyCode { get; set; }
        public DateTime Day { get; set; }
        public string Ip { get; set; }
        /// <summary>Pages viewed from this IP that day.</summary>
        public int Views { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        /// <summary>Address of the last page seen, without the host.</summary>
        public string LastPage { get; set; }
        public string UserAgent { get; set; }
    }

    /// <summary>Views and different visitors of a day or a month.</summary>
    public class VisitCount
    {
        public DateTime Start { get; set; }
        public string Label { get; set; }
        public int Views { get; set; }
        public int Visitors { get; set; }
    }

    /// <summary>The visit report of a site (or of all the stores together) shown in the panels.</summary>
    public class VisitReport
    {
        public string Title { get; set; }
        public VisitCount Today { get; set; }
        public VisitCount Yesterday { get; set; }
        public VisitCount ThisMonth { get; set; }
        public VisitCount LastMonth { get; set; }
        /// <summary>The last 30 days, oldest first; days without visits are included with zeros.</summary>
        public List<VisitCount> Days { get; set; }
        /// <summary>The last 12 Persian months, oldest first.</summary>
        public List<VisitCount> Months { get; set; }
    }

    /// <summary>A store in the admin's ranking of visits.</summary>
    public class StoreVisitRow
    {
        public string CompanyCode { get; set; }
        public string Name { get; set; }
        public bool IsDemo { get; set; }
        public int TodayViews { get; set; }
        public int MonthViews { get; set; }
        public int MonthVisitors { get; set; }
    }
}
