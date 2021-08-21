using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace PushNotification.ApnFcmHttpV2
{
    /// <summary>
    ///   _fcmClient = new FcmClientSendToSingleRegistrationId(fcm_config);
    ///  var sentresult = await _fcmClient.Send(message.DeviceToken, message.Title, message.Body, message.Message);
    ///         result = sentresult.Ok;
    /// 
    /// </summary>
    public class FCMsClient : IDisposable
    {

        public FCMsClient(GcmConfiguration configuration)
        {
            Configuration = configuration;
            _http = new HttpClient();

            _http.DefaultRequestHeaders.UserAgent.Clear();
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FcmClient", "3.0.1"));
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "key=" + Configuration.SenderAuthToken);

            _httpCheckToken = new HttpClient();

            _httpCheckToken.DefaultRequestHeaders.UserAgent.Clear();
            _httpCheckToken.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FcmClient", "3.0.1"));
            _httpCheckToken.DefaultRequestHeaders.Remove("Authorization");
            _httpCheckToken.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "key=" + Configuration.SenderAuthToken);
        }

        public GcmConfiguration Configuration { get; private set; }

        HttpClient _http;

        HttpClient _httpCheckToken;
        public string CheckToken(string token)
        {
            //
            return string.Empty;
            // return  await (await _httpCheckToken.SendAsync($"https://iid.googleapis.com/iid/info/{token}", HttpMethod.Get)).Content.ReadAsStringAsync();
        }
        //public async Task<SendInternalResult> Send(string deviceIdOrFcmToken, string notiTitle, string notiBody, JObject message)
        //{
        //    var payloadData = message;

        //    payloadData["title"] = notiTitle;
        //    payloadData["body"] = notiBody;

        //    var r = await SendInternal(new GcmNotification
        //    {
        //        RegistrationIds = new List<string> { deviceIdOrFcmToken },
        //        Data = payloadData
        //    });

        //    return r;
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceIdOrFcmToken"></param>
        /// <param name="notiTitle"></param>
        /// <param name="notiBody"></param>
        /// <param name="dataJson"></param>
        /// <returns></returns>
        public async Task<SendInternalResult> Send(string deviceIdOrFcmToken, string notiTitle, string notiBody, string dataJson)
        {
            return await Send(new List<string> { deviceIdOrFcmToken }, notiTitle, notiBody, dataJson);
        }
        public async Task<SendInternalResult> Send(List<string> deviceIdOrFcmTokens, string notiTitle, string notiBody, string dataJson)
        {

            var payloadData = JObject.Parse(dataJson);
            //payloadData["title"] = notiTitle;
            //payloadData["body"] = notiBody;
            var msg = new GcmNotification
            {
                RegistrationIds = deviceIdOrFcmTokens,
                Data = payloadData,
            };

            if (!string.IsNullOrEmpty(notiTitle) && !string.IsNullOrEmpty(notiBody))
            {
                var notiObject = new JObject();
                //notiObject["title"] = notiTitle;
                //notiObject["body"] = notiBody;
                msg.Notification = notiObject;
            }

            var r = await SendInternal(msg);

            return r;
        }
        async Task<SendInternalResult> SendInternal(GcmNotification notification)
        {
            var str = string.Empty;
            try
            {
                var json = notification.GetJson();

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(Configuration.GcmUrl, content);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine($"System.Net.HttpStatusCode.Unauthorized {notification}");
                }

                str = await response.Content.ReadAsStringAsync();

                var responseJson = JObject.Parse(str);

                var result = new GcmResponse()
                {
                    ResponseCode = GcmResponseCode.Ok,
                    OriginalNotification = notification
                };
                result.NumberOfCanonicalIds = responseJson.Value<long>("canonical_ids");
                result.NumberOfFailures = responseJson.Value<long>("failure");
                result.NumberOfSuccesses = responseJson.Value<long>("success");

                var jsonResults = responseJson["results"] as JArray ?? new JArray();
                foreach (var r in jsonResults)
                {
                    var msgResult = new GcmMessageResult();

                    msgResult.MessageId = r.Value<string>("message_id");
                    msgResult.CanonicalRegistrationId = r.Value<string>("registration_id");
                    msgResult.ResponseStatus = GcmResponseStatus.Ok;

                    if (!string.IsNullOrEmpty(msgResult.CanonicalRegistrationId))
                        msgResult.ResponseStatus = GcmResponseStatus.CanonicalRegistrationId;
                    else if (r["error"] != null)
                    {
                        var err = r.Value<string>("error") ?? "";

                        msgResult.ResponseStatus = GetGcmResponseStatus(err);
                    }

                    result.Results.Add(msgResult);
                }
                var firstResult = result.Results.FirstOrDefault();

                if (response.IsSuccessStatusCode && firstResult != null && firstResult.ResponseStatus == GcmResponseStatus.Ok)
                { // Success
                    return new SendInternalResult { Ok = true, rawResult = str };
                }


                return new SendInternalResult { Ok = false, rawResult = str };
            }
            catch (Exception ex)
            {
                return new SendInternalResult { Ok = false, rawResult = str + " " + ex.Message + ex.StackTrace };
            }

        }

        static GcmResponseStatus GetGcmResponseStatus(string str)
        {
            var enumType = typeof(GcmResponseStatus);

            foreach (var name in Enum.GetNames(enumType))
            {
                var enumMemberAttribute = ((EnumMemberAttribute[])enumType.GetField(name).GetCustomAttributes(typeof(EnumMemberAttribute), true)).Single();

                if (enumMemberAttribute.Value.Equals(str, StringComparison.InvariantCultureIgnoreCase))
                    return (GcmResponseStatus)Enum.Parse(enumType, name);
            }

            //Default
            return GcmResponseStatus.Error;
        }

        public void Dispose()
        {
            _http = null;
        }
    }


    public enum GcmResponseCode
    {
        Ok,
        Error,
        BadRequest,
        ServiceUnavailable,
        InvalidAuthToken,
        InternalServiceError
    }

    public class GcmResponse
    {
        public GcmResponse()
        {
            MulticastId = -1;
            NumberOfSuccesses = 0;
            NumberOfFailures = 0;
            NumberOfCanonicalIds = 0;
            OriginalNotification = null;
            Results = new List<GcmMessageResult>();
            ResponseCode = GcmResponseCode.Ok;
        }

        [JsonProperty("multicast_id")]
        public long MulticastId { get; set; }

        [JsonProperty("success")]
        public long NumberOfSuccesses { get; set; }

        [JsonProperty("failure")]
        public long NumberOfFailures { get; set; }

        [JsonProperty("canonical_ids")]
        public long NumberOfCanonicalIds { get; set; }

        [JsonIgnore]
        public GcmNotification OriginalNotification { get; set; }

        [JsonProperty("results")]
        public List<GcmMessageResult> Results { get; set; }

        [JsonIgnore]
        public GcmResponseCode ResponseCode { get; set; }
    }
    public interface INotification
    {
        bool IsDeviceRegistrationIdValid();
        object Tag { get; set; }
    }
    public class GcmNotification : INotification
    {
        public static GcmNotification ForSingleResult(GcmResponse response, int resultIndex)
        {
            var result = new GcmNotification();
            result.Tag = response.OriginalNotification.Tag;
            result.MessageId = response.OriginalNotification.MessageId;

            if (response.OriginalNotification.RegistrationIds != null && response.OriginalNotification.RegistrationIds.Count >= (resultIndex + 1))
                result.RegistrationIds.Add(response.OriginalNotification.RegistrationIds[resultIndex]);

            result.CollapseKey = response.OriginalNotification.CollapseKey;
            result.Data = response.OriginalNotification.Data;
            result.DelayWhileIdle = response.OriginalNotification.DelayWhileIdle;
            result.ContentAvailable = response.OriginalNotification.ContentAvailable;
            result.DryRun = response.OriginalNotification.DryRun;
            result.Priority = response.OriginalNotification.Priority;
            result.To = response.OriginalNotification.To;
            result.NotificationKey = response.OriginalNotification.NotificationKey;

            return result;
        }

        public static GcmNotification ForSingleRegistrationId(GcmNotification msg, string registrationId)
        {
            var result = new GcmNotification();
            result.Tag = msg.Tag;
            result.MessageId = msg.MessageId;
            result.RegistrationIds.Add(registrationId);
            result.To = null;
            result.CollapseKey = msg.CollapseKey;
            result.Data = msg.Data;
            result.DelayWhileIdle = msg.DelayWhileIdle;
            result.ContentAvailable = msg.ContentAvailable;
            result.DryRun = msg.DryRun;
            result.Priority = msg.Priority;
            result.NotificationKey = msg.NotificationKey;

            return result;
        }

        public GcmNotification()
        {
            RegistrationIds = new List<string>();
            CollapseKey = string.Empty;
            Data = null;
            DelayWhileIdle = null;
        }

        public bool IsDeviceRegistrationIdValid()
        {
            return RegistrationIds != null && RegistrationIds.Any();
        }

        [JsonIgnore]
        public object Tag { get; set; }

        [JsonProperty("message_id")]
        public string MessageId { get; internal set; }

        /// <summary>
        /// Registration ID of the Device(s).  Maximum of 1000 registration Id's per notification.
        /// </summary>
        [JsonProperty("registration_ids")]
        public List<string> RegistrationIds { get; set; }

        /// <summary>
        /// Registration ID or Group/Topic to send notification to.  Overrides RegsitrationIds.
        /// </summary>
        /// <value>To.</value>
        [JsonProperty("to")]
        public string To { get; set; }

        /// <summary>
        /// Only the latest message with the same collapse key will be delivered
        /// </summary>
        [JsonProperty("collapse_key")]
        public string CollapseKey { get; set; }

        /// <summary>
        /// JSON Payload to be sent in the message
        /// </summary>
        [JsonProperty("data")]
        public JObject Data { get; set; }

        /// <summary>
        /// Notification JSON payload
        /// </summary>
        /// <value>The notification payload.</value>
        [JsonProperty("notification")]
        public JObject Notification { get; set; }

        /// <summary>
        /// If true, GCM will only be delivered once the device's screen is on
        /// </summary>
        [JsonProperty("delay_while_idle")]
        public bool? DelayWhileIdle { get; set; }

        /// <summary>
        /// Time in seconds that a message should be kept on the server if the device is offline.  Default (and maximum) is 4 weeks.
        /// </summary>
        [JsonProperty("time_to_live")]
        public int? TimeToLive { get; set; }

        /// <summary>
        /// If true, dry_run attribute will be sent in payload causing the notification not to be actually sent, but the result returned simulating the message
        /// </summary>
        [JsonProperty("dry_run")]
        public bool? DryRun { get; set; }

        /// <summary>
        /// A string that maps a single user to multiple registration IDs associated with that user. This allows a 3rd-party server to send a single message to multiple app instances (typically on multiple devices) owned by a single user.
        /// </summary>
        [Obsolete("Deprecated on GCM Server API.  Use Device Group Messaging to send to multiple devices.")]
        public string NotificationKey { get; set; }

        /// <summary>
        /// A string containing the package name of your application. When set, messages will only be sent to registration IDs that match the package name
        /// </summary>
        [JsonProperty("restricted_package_name")]
        public string RestrictedPackageName { get; set; }

        /// <summary>
        /// On iOS, use this field to represent content-available in the APNS payload. When a notification or message is sent and this is set to true, an inactive client app is awoken. On Android, data messages wake the app by default. On Chrome, currently not supported.
        /// </summary>
        /// <value>The content available.</value>
        [JsonProperty("content_available")]
        public bool? ContentAvailable { get; set; }

        /// <summary>
        /// Corresponds to iOS APNS priorities (Normal is 5 and high is 10).  Default is Normal.
        /// </summary>
        /// <value>The priority.</value>
        [JsonProperty("priority"), JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public GcmNotificationPriority? Priority { get; set; }

        public string GetJson()
        {
            // If 'To' was used instead of RegistrationIds, let's make RegistrationId's null
            // so we don't serialize an empty array for this property
            // otherwise, google will complain that we specified both instead
            if (RegistrationIds != null && RegistrationIds.Count <= 0 && !string.IsNullOrEmpty(To))
                RegistrationIds = null;

            // Ignore null values
            return JsonConvert.SerializeObject(this,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        public override string ToString()
        {
            return GetJson();
        }
    }
    public enum GcmNotificationPriority
    {
        [EnumMember(Value = "normal")]
        Normal = 5,
        [EnumMember(Value = "high")]
        High = 10
    }

    public class GcmMessageResult
    {
        [JsonProperty("message_id", NullValueHandling = NullValueHandling.Ignore)]
        public string MessageId { get; set; }

        [JsonProperty("registration_id", NullValueHandling = NullValueHandling.Ignore)]
        public string CanonicalRegistrationId { get; set; }

        [JsonIgnore]
        public GcmResponseStatus ResponseStatus { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error
        {
            get
            {
                switch (ResponseStatus)
                {
                    case GcmResponseStatus.Ok:
                        return null;
                    case GcmResponseStatus.Unavailable:
                        return "Unavailable";
                    case GcmResponseStatus.QuotaExceeded:
                        return "QuotaExceeded";
                    case GcmResponseStatus.NotRegistered:
                        return "NotRegistered";
                    case GcmResponseStatus.MissingRegistrationId:
                        return "MissingRegistration";
                    case GcmResponseStatus.MissingCollapseKey:
                        return "MissingCollapseKey";
                    case GcmResponseStatus.MismatchSenderId:
                        return "MismatchSenderId";
                    case GcmResponseStatus.MessageTooBig:
                        return "MessageTooBig";
                    case GcmResponseStatus.InvalidTtl:
                        return "InvalidTtl";
                    case GcmResponseStatus.InvalidRegistration:
                        return "InvalidRegistration";
                    case GcmResponseStatus.InvalidDataKey:
                        return "InvalidDataKey";
                    case GcmResponseStatus.InternalServerError:
                        return "InternalServerError";
                    case GcmResponseStatus.DeviceQuotaExceeded:
                        return null;
                    case GcmResponseStatus.CanonicalRegistrationId:
                        return null;
                    case GcmResponseStatus.Error:
                        return "Error";
                    default:
                        return null;
                }
            }
        }
    }


    public enum GcmResponseStatus
    {
        [EnumMember(Value = "Ok")]
        Ok,

        [EnumMember(Value = "Error")]
        Error,

        [EnumMember(Value = "QuotaExceeded")]
        QuotaExceeded,

        [EnumMember(Value = "DeviceQuotaExceeded")]
        DeviceQuotaExceeded,

        [EnumMember(Value = "InvalidRegistration")]
        InvalidRegistration,

        [EnumMember(Value = "NotRegistered")]
        NotRegistered,

        [EnumMember(Value = "MessageTooBig")]
        MessageTooBig,

        [EnumMember(Value = "MissingCollapseKey")]
        MissingCollapseKey,

        [EnumMember(Value = "MissingRegistration")]
        MissingRegistrationId,

        [EnumMember(Value = "Unavailable")]
        Unavailable,

        [EnumMember(Value = "MismatchSenderId")]
        MismatchSenderId,

        [EnumMember(Value = "CanonicalRegistrationId")]
        CanonicalRegistrationId,

        [EnumMember(Value = "InvalidDataKey")]
        InvalidDataKey,

        [EnumMember(Value = "InvalidTtl")]
        InvalidTtl,

        [EnumMember(Value = "InternalServerError")]
        InternalServerError,

        [EnumMember(Value = "InvalidPackageName")]
        InvalidPackageName
    }


    public class GcmConfiguration
    {
        private const string GCM_SEND_URL = "https://gcm-http.googleapis.com/gcm/send";

        public GcmConfiguration(string senderAuthTokenOrApiKey)
        {
            this.SenderAuthToken = senderAuthTokenOrApiKey;
            this.GcmUrl = GCM_SEND_URL;

            this.ValidateServerCertificate = false;
        }

        public GcmConfiguration(string optionalSenderID, string senderAuthToken, string optionalApplicationIdPackageName)
        {
            this.SenderID = optionalSenderID;
            this.SenderAuthToken = senderAuthToken;
            this.ApplicationIdPackageName = optionalApplicationIdPackageName;
            this.GcmUrl = GCM_SEND_URL;

            this.ValidateServerCertificate = false;
        }

        public string SenderID { get; private set; }

        public string SenderAuthToken { get; private set; }

        public string ApplicationIdPackageName { get; private set; }

        public bool ValidateServerCertificate { get; set; }

        public string GcmUrl { get; set; }

        public void OverrideUrl(string url)
        {
            GcmUrl = url;
        }
    }
}
