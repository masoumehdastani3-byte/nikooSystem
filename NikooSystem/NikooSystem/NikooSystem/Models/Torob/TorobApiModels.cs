using System.Collections.Generic;
using Newtonsoft.Json;

namespace NikooSystem.Models.Torob
{
    public class TorobProductsResponse
    {
        [JsonProperty("api_version")]
        public string ApiVersion { get; set; } = "torob_api_v3";

        [JsonProperty("current_page")]
        public int? CurrentPage { get; set; }

        [JsonProperty("total")]
        public int? Total { get; set; }

        [JsonProperty("max_pages")]
        public int? MaxPages { get; set; }

        [JsonProperty("next_cursor")]
        public string NextCursor { get; set; }

        [JsonProperty("products")]
        public List<TorobProductDto> Products { get; set; } = new List<TorobProductDto>();
    }

    public class TorobProductDto
    {
        [JsonProperty("page_unique")]
        public string PageUnique { get; set; }

        [JsonProperty("page_url")]
        public string PageUrl { get; set; }

        [JsonProperty("product_group_id")]
        public string ProductGroupId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("subtitle")]
        public string Subtitle { get; set; }

        [JsonProperty("current_price")]
        public long CurrentPrice { get; set; }

        [JsonProperty("old_price")]
        public long? OldPrice { get; set; }

        [JsonProperty("availability")]
        public bool Availability { get; set; }

        [JsonProperty("category_name")]
        public string CategoryName { get; set; }

        [JsonProperty("image_links")]
        public List<string> ImageLinks { get; set; } = new List<string>();

        [JsonProperty("short_desc")]
        public string ShortDesc { get; set; }

        [JsonProperty("spec")]
        public Dictionary<string, string> Spec { get; set; } = new Dictionary<string, string>();

        [JsonProperty("date_added")]
        public string DateAdded { get; set; }

        [JsonProperty("date_updated")]
        public string DateUpdated { get; set; }
    }

    public class TorobErrorResponse
    {
        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
