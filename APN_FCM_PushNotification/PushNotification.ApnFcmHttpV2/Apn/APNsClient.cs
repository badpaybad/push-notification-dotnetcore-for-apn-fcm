
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PushNotification.ApnFcmHttpV2
{
    /// <summary>
    ///   using (var apnCli = new APNsClient(_configuration.Certificate,
    ///                _configuration.ServerEnvironment== ApnsConfiguration.ApnsServerEnvironment.Sandbox))
    ///            {

    ///                dynamic data = JsonConvert.DeserializeObject<dynamic>(message);
    ///    APNsClient.ApnsResponse rn = await apnCli.SendAsync(deviveToken, notiTitle, notiBody, data);

    ///                if (rn.IsSuccessful)
    ///                {
    ///                    return new SendInternalResult { Ok = true, rawResult = rn.ReasonString
    ///};
    ///                }
    ///                else
    ///{
    ///    return new SendInternalResult { Ok = false, rawResult = rn.ReasonString };
    ///}

    ///            }
    /// </summary>
    public class APNsClient : IDisposable
    {
        HttpClient _http;
        string _bundleId;
        bool _useSandbox;

        string _urlSingleDeviceId;

        internal const string _developmentEndpoint = "https://api.development.push.apple.com";
        internal const string _productionEndpoint = "https://api.push.apple.com";


        public APNsClient(X509Certificate2 cert, bool useSandbox)
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;

            handler.ClientCertificates.Add(cert);
            _http = new HttpClient(handler);

            var split = cert.Subject.Split(new[] { "0.9.2342.19200300.100.1.1=" }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length != 2)
            {
                // On Linux .NET Core cert.Subject prints `userId=xxx` instead of `0.9.2342.19200300.100.1.1=xxx`
                split = cert.Subject.Split(new[] { "userId=" }, StringSplitOptions.RemoveEmptyEntries);
            }
            if (split.Length != 2)
            {
                // if subject prints `uid=xxx` instead of `0.9.2342.19200300.100.1.1=xxx`
                split = cert.Subject.Split(new[] { "uid=" }, StringSplitOptions.RemoveEmptyEntries);
            }

            if (split.Length != 2)
                throw new InvalidOperationException("Provided certificate does not appear to be a valid APNs certificate.");

            _bundleId = split[1].Replace(".voip", "");
            _useSandbox = useSandbox;

            _urlSingleDeviceId = (_useSandbox ? _developmentEndpoint : _productionEndpoint)
               + ":2197"
               + "/3/device/";
        }
        public APNsClient(string pathToCert, string pwdCert, bool useSandbox)
            : this(new X509Certificate2(pathToCert, pwdCert), useSandbox)
        {

        }
        public APNsClient(ApnsConfiguration apnsConfiguration) :
            this(apnsConfiguration.Certificate, apnsConfiguration.ServerEnvironment == ApnsConfiguration.ApnsServerEnvironment.Sandbox)
        {

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="title"></param>
        /// <param name="body"></param>
        /// <param name="dataJson">todo: dynamic data : bad here</param>
        /// <param name="badge"></param>
        /// <param name="sound"></param>
        /// <param name="category"></param>
        /// <returns></returns>
        public async Task<SendInternalResult> Send(string deviceId, string title, string body, string dataJson, int badge = 1, string sound = "default", string category = "")
        {
            var rn = await SendInternal(deviceId, title, body, dataJson, badge, sound, category);

            if (rn.IsSuccessful)
            {
                return new SendInternalResult { Ok = true, rawResult = rn.ReasonString };
            }
            else
            {
                return new SendInternalResult { Ok = false, rawResult = rn.ReasonString };
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="title"></param>
        /// <param name="body"></param>
        /// <param name="dataJson">todo: dynamic data : bad here</param>
        /// <param name="badge"></param>
        /// <param name="sound"></param>
        /// <param name="category"></param>
        /// <returns></returns>
        async Task<ApnsResponse> SendInternal(string deviceId, string title, string body, string dataJson, int badge = 1, string sound = "default", string category = "")
        {
            var url = $"{_urlSingleDeviceId}{deviceId}";

            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Version = new Version(2, 0);
            req.Headers.Add("apns-priority", "10");
            req.Headers.Add("apns-push-type", "alert");
            req.Headers.Add("apns-topic", _bundleId);

            dynamic payload = new ExpandoObject();
            payload.aps = new ExpandoObject();
            payload.aps.alert = new { title = title, body = body };
            payload.aps.badge = badge;
            payload.aps.sound = sound;

            if (!string.IsNullOrEmpty(category))
                payload.aps.category = category;

            payload.data = JsonConvert.DeserializeObject<dynamic>(dataJson);

            req.Content = new JsonContent(payload);

            HttpResponseMessage resp;
            try
            {
                CancellationToken ct = default;
                resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (ex.InnerException != null
                 && (ex.InnerException is AuthenticationException || ex.InnerException is IOException))
                    throw new ApnsCertificateExpiredException(innerException: ex);

                return ApnsResponse.Error(ApnsResponseReason.Unknown, ex.Message);
            }

            var respContent = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Process status codes specified by APNs documentation
            // https://developer.apple.com/documentation/usernotifications/setting_up_a_remote_notification_server/handling_notification_responses_from_apns
            var statusCode = (int)resp.StatusCode;

            // Push has been successfully sent. This is the only code indicating a success as per documentation.
            if (statusCode == 200)
                return ApnsResponse.Successful();

            // something went wrong
            // check for payload 
            // {"reason":"DeviceTokenNotForTopic"}
            // {"reason":"Unregistered","timestamp":1454948015990}

            ApnsErrorResponsePayload errorPayload;
            try
            {
                errorPayload = JsonConvert.DeserializeObject<ApnsErrorResponsePayload>(respContent);

                return ApnsResponse.Error(errorPayload.Reason, errorPayload.ReasonRaw);
            }
            catch (JsonException ex)
            {
                return ApnsResponse.Error(ApnsResponseReason.Unknown,
                    $"Status: {statusCode}, reason: {respContent ?? "not specified"}. " + ex.Message);
            }
        }

        public void Dispose()
        {
            _http = null;
        }


    }
    public enum ApnsResponseReason
    {
        Unknown = -1,

        // 200
        Success,

        // 400
        BadCollapseId,
        BadDeviceToken,
        BadExpirationDate,
        BadMessageId,
        BadPriority,
        BadTopic,
        DeviceTokenNotForTopic,
        DuplicateHeaders,
        IdleTimeout,
        InvalidPushType,
        MissingDeviceToken,
        MissingTopic,
        PayloadEmpty,
        TopicDisallowed,

        // 403
        BadCertificate,
        BadCertificateEnvironment,
        ExpiredProviderToken,
        Forbidden,
        InvalidProviderToken,
        MissingProviderToken,

        // 404
        BadPath,

        // 405
        MethodNotAllowed,

        // 410
        Unregistered,

        // 413
        PayloadTooLarge,

        // 429
        TooManyProviderTokenUpdates,
        TooManyRequests,

        // 500
        InternalServerError,

        // 503
        ServiceUnavailable,
        Shutdown
    }
    public class ApnsResponse
    {
        public ApnsResponseReason Reason { get; }
        public string ReasonString { get; }
        public bool IsSuccessful { get; }

        [JsonConstructor]
        ApnsResponse(ApnsResponseReason reason, string reasonString, bool isSuccessful)
        {
            Reason = reason;
            ReasonString = reasonString;
            IsSuccessful = isSuccessful;
        }

        public static ApnsResponse Successful() { return new ApnsResponse(ApnsResponseReason.Success, null, true); }

        public static ApnsResponse Error(ApnsResponseReason reason, string reasonString) => new ApnsResponse(reason, reasonString, false);
    }
    public class ApnsErrorResponsePayload
    {
        [JsonIgnore]
        public ApnsResponseReason Reason =>
            Enum.TryParse<ApnsResponseReason>(ReasonRaw, out var value)
            ? value : ApnsResponseReason.Unknown;

        [JsonProperty("reason")]
        public string ReasonRaw { get; set; }

        [JsonConverter(typeof(UnixTimestampMillisecondsJsonConverter))] // timestamp is in milliseconds (https://openradar.appspot.com/24548417)
        public DateTimeOffset? Timestamp { get; set; }
    }
    public class UnixTimestampMillisecondsJsonConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset ReadJson(JsonReader reader, Type objectType, DateTimeOffset existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)reader.Value);
        }

        public override void WriteJson(JsonWriter writer, DateTimeOffset value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanWrite { get; } = false;
    }
    public class JsonContent : StringContent
    {
        const string JsonMediaType = "application/json";

        public JsonContent(object obj) : this(obj is string str ? str : JsonConvert.SerializeObject(obj, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }))
        {

        }

        JsonContent(string content) : base(content, Encoding.UTF8, JsonMediaType)
        {
        }

        JsonContent(string content, Encoding encoding) : base(content, encoding, JsonMediaType)
        {
        }

        JsonContent(string content, Encoding encoding, string mediaType) : base(content, encoding, JsonMediaType)
        {

        }
    }
    public class ApnsCertificateExpiredException : Exception
    {
        const string ConstMessage = "Your APNs certificate has expired. Please renew it at. More info: https://developer.apple.com/documentation/usernotifications/setting_up_a_remote_notification_server/establishing_a_certificate-based_connection_to_apns";

        public ApnsCertificateExpiredException(string message = ConstMessage, Exception innerException = null) : base(ConstMessage, innerException)
        {
        }
    }
    public class ApnsConfiguration
    {
        #region Constants
        const string APNS_SANDBOX_HOST = "gateway.sandbox.push.apple.com";
        const string APNS_PRODUCTION_HOST = "gateway.push.apple.com";

        const string APNS_SANDBOX_FEEDBACK_HOST = "feedback.sandbox.push.apple.com";
        const string APNS_PRODUCTION_FEEDBACK_HOST = "feedback.push.apple.com";

        const int APNS_SANDBOX_PORT = 2195;
        const int APNS_PRODUCTION_PORT = 2195;

        const int APNS_SANDBOX_FEEDBACK_PORT = 2196;
        const int APNS_PRODUCTION_FEEDBACK_PORT = 2196;

        #endregion

        public ApnsConfiguration(ApnsServerEnvironment serverEnvironment, string certificateFile, string certificateFilePwd, bool validateIsApnsCertificate)
            : this(serverEnvironment, System.IO.File.ReadAllBytes(certificateFile), certificateFilePwd, validateIsApnsCertificate)
        {
        }

        public ApnsConfiguration(ApnsServerEnvironment serverEnvironment, string certificateFile, string certificateFilePwd)
            : this(serverEnvironment, System.IO.File.ReadAllBytes(certificateFile), certificateFilePwd)
        {
        }

        public ApnsConfiguration(ApnsServerEnvironment serverEnvironment, byte[] certificateData, string certificateFilePwd, bool validateIsApnsCertificate)
            : this(serverEnvironment, new X509Certificate2(certificateData, certificateFilePwd,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable), validateIsApnsCertificate)
        {
        }

        public ApnsConfiguration(ApnsServerEnvironment serverEnvironment, byte[] certificateData, string certificateFilePwd)
            : this(serverEnvironment, new X509Certificate2(certificateData, certificateFilePwd,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable))
        {
        }

        public ApnsConfiguration(string overrideHost, int overridePort, bool skipSsl = true)
        {
            SkipSsl = skipSsl;

            Initialize(ApnsServerEnvironment.Sandbox, null, false);

            OverrideServer(overrideHost, overridePort);
        }

        public ApnsConfiguration(ApnsServerEnvironment serverEnvironment, X509Certificate2 certificate)
        {
            Initialize(serverEnvironment, certificate, true);
        }

        public ApnsConfiguration(ApnsServerEnvironment serverEnvironment, X509Certificate2 certificate, bool validateIsApnsCertificate)
        {
            Initialize(serverEnvironment, certificate, validateIsApnsCertificate);
        }

        void Initialize(ApnsServerEnvironment serverEnvironment, X509Certificate2 certificate, bool validateIsApnsCertificate)
        {
            ServerEnvironment = serverEnvironment;

            var production = serverEnvironment == ApnsServerEnvironment.Production;

            Host = production ? APNS_PRODUCTION_HOST : APNS_SANDBOX_HOST;
            FeedbackHost = production ? APNS_PRODUCTION_FEEDBACK_HOST : APNS_SANDBOX_FEEDBACK_HOST;
            Port = production ? APNS_PRODUCTION_PORT : APNS_SANDBOX_PORT;
            FeedbackPort = production ? APNS_PRODUCTION_FEEDBACK_PORT : APNS_SANDBOX_FEEDBACK_PORT;

            Certificate = certificate;

            MillisecondsToWaitBeforeMessageDeclaredSuccess = 3000;
            ConnectionTimeout = 10000;
            MaxConnectionAttempts = 3;

            FeedbackIntervalMinutes = 10;
            FeedbackTimeIsUTC = false;

            AdditionalCertificates = new List<X509Certificate2>();
            AddLocalAndMachineCertificateStores = false;

            if (validateIsApnsCertificate)
                CheckIsApnsCertificate();

            ValidateServerCertificate = false;

            KeepAlivePeriod = TimeSpan.FromMinutes(20);
            KeepAliveRetryPeriod = TimeSpan.FromSeconds(30);

            InternalBatchSize = 1000;
            InternalBatchingWaitPeriod = TimeSpan.FromMilliseconds(750);

            InternalBatchFailureRetryCount = 1;
        }


        void CheckIsApnsCertificate()
        {
            if (Certificate != null)
            {
                var issuerName = Certificate.IssuerName.Name;
                var commonName = Certificate.SubjectName.Name;

                if (!issuerName.Contains("Apple"))
                    throw new ArgumentOutOfRangeException("Your Certificate does not appear to be issued by Apple!  Please check to ensure you have the correct certificate!");

                if (!Regex.IsMatch(commonName, "Apple.*?Push Services")
                    && !commonName.Contains("Website Push ID:"))
                    throw new ArgumentOutOfRangeException("Your Certificate is not a valid certificate for connecting to Apple's APNS servers");

                if (commonName.Contains("Development") && ServerEnvironment != ApnsServerEnvironment.Sandbox)
                    throw new ArgumentOutOfRangeException("You are using a certificate created for connecting only to the Sandbox APNS server but have selected a different server environment to connect to.");

                if (commonName.Contains("Production") && ServerEnvironment != ApnsServerEnvironment.Production)
                    throw new ArgumentOutOfRangeException("You are using a certificate created for connecting only to the Production APNS server but have selected a different server environment to connect to.");
            }
            else
            {
                throw new ArgumentOutOfRangeException("You must provide a Certificate to connect to APNS with!");
            }
        }

        public void OverrideServer(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public void OverrideFeedbackServer(string host, int port)
        {
            FeedbackHost = host;
            FeedbackPort = port;
        }

        public string Host { get; private set; }

        public int Port { get; private set; }

        public string FeedbackHost { get; private set; }

        public int FeedbackPort { get; private set; }

        public X509Certificate2 Certificate { get; private set; }

        public List<X509Certificate2> AdditionalCertificates { get; private set; }

        public bool AddLocalAndMachineCertificateStores { get; set; }

        public bool SkipSsl { get; set; }

        public int MillisecondsToWaitBeforeMessageDeclaredSuccess { get; set; }

        public int FeedbackIntervalMinutes { get; set; }

        public bool FeedbackTimeIsUTC { get; set; }

        public bool ValidateServerCertificate { get; set; }

        public int ConnectionTimeout { get; set; }

        public int MaxConnectionAttempts { get; set; }

        /// <summary>
        /// The internal connection to APNS servers batches notifications to send before waiting for errors for a short time.
        /// This value will set a maximum size per batch.  The default value is 1000.  You probably do not want this higher than 7500.
        /// </summary>
        /// <value>The size of the internal batch.</value>
        public int InternalBatchSize { get; set; }

        /// <summary>
        /// How long the internal connection to APNS servers should idle while collecting notifications in a batch to send.
        /// Setting this value too low might result in many smaller batches being used.
        /// </summary>
        /// <value>The internal batching wait period.</value>
        public TimeSpan InternalBatchingWaitPeriod { get; set; }

        /// <summary>
        /// How many times the internal batch will retry to send in case of network failure. The default value is 1.
        /// </summary>
        /// <value>The internal batch failure retry count.</value>
        public int InternalBatchFailureRetryCount { get; set; }

        /// <summary>
        /// Gets or sets the keep alive period to set on the APNS socket
        /// </summary>
        public TimeSpan KeepAlivePeriod { get; set; }

        /// <summary>
        /// Gets or sets the keep alive retry period to set on the APNS socket
        /// </summary>
        public TimeSpan KeepAliveRetryPeriod { get; set; }

        /// <summary>
        /// Gets the configured APNS server environment 
        /// </summary>
        /// <value>The server environment.</value>
        public ApnsServerEnvironment ServerEnvironment { get; private set; }

        public enum ApnsServerEnvironment
        {
            Sandbox,
            Production
        }
    }

}
