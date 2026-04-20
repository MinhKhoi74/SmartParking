namespace SmartParking.DTOs
{
    using System.Text.Json.Serialization;
    using System.ComponentModel.DataAnnotations;

    public class CheckInRequest
    {
        [Required(ErrorMessage = "Biển số xe không được để trống")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Biển số phải từ 3-50 ký tự")]
        [JsonPropertyName("plateNumber")]
        public string PlateNumber { get; set; }

        [JsonPropertyName("stationId")]
        public string? StationId { get; set; }

        [Range(0, 1, ErrorMessage = "Confidence phải từ 0 đến 1")]
        [JsonPropertyName("confidence")]
        public float Confidence { get; set; } = 0.9f;

        [JsonPropertyName("imageBase64")]
        public string? ImageBase64 { get; set; }
    }

    public class CheckInResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("checkInId")]
        public int? CheckInId { get; set; }

        [JsonPropertyName("checkInTime")]
        public DateTime? CheckInTime { get; set; }
    }

    public class CheckOutRequest
    {
        [Required(ErrorMessage = "Biển số xe không được để trống")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Biển số phải từ 3-50 ký tự")]
        [JsonPropertyName("plateNumber")]
        public string PlateNumber { get; set; }

        [JsonPropertyName("stationId")]
        public string? StationId { get; set; }

        [JsonPropertyName("imageBase64")]
        public string? ImageBase64 { get; set; }
    }

    public class CheckOutResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("checkOutId")]
        public int? CheckOutId { get; set; }

        [JsonPropertyName("checkOutTime")]
        public DateTime? CheckOutTime { get; set; }

        [JsonPropertyName("durationMinutes")]
        public int? DurationMinutes { get; set; }

        [JsonPropertyName("feeAmount")]
        public decimal? FeeAmount { get; set; }
    }
}
