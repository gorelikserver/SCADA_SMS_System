namespace SCADASMSSystem.Web.Models
{
    public class SmsSettings
    {
        public string ApiEndpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = "POST"; // GET, POST, PUT, PATCH, etc.
        public string ContentType { get; set; } = "application/x-www-form-urlencoded"; // or application/json
        public string ApiParams { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public int RateLimit { get; set; } = 10;
        public int RateWindow { get; set; } = 60;
        public int DuplicateWindow { get; set; } = 5;
        
        /// <summary>
        /// SCADA PCIM Object ID - varies by deployment (e.g., 198 for some installations).
        /// Used in alarm action commands to reference SCADA objects.
        /// </summary>
        public int ScadaPcimObjectId { get; set; } = 198; // Default value for backward compatibility
    }
}