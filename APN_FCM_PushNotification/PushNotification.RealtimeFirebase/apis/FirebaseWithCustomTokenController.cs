using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PushNotification.RealtimeFirebase.apis
{
    [Route("api/[controller]")]
    [ApiController]
    public class FirebaseWithCustomTokenController : ControllerBase
    {

        static FirebaseAdmin.Auth.FirebaseAuth _auth;

        static FirebaseApp _app;
        static GoogleCredential _googleCredential;

        Microsoft.Extensions.Configuration.IConfiguration _configuration;
        public FirebaseWithCustomTokenController(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _configuration = configuration;
            if (_googleCredential == null)
            {
                // check : firebaseadmin.png how to get json file for credential 

                var fileCredential = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _configuration.GetSection("FireBaseAdminFileJson").Value);
                _googleCredential = GoogleCredential.FromFile(fileCredential)
                               .CreateScoped(
                                   new[] {
                                        "https://www.googleapis.com/auth/firebase.database",
                                        "https://www.googleapis.com/auth/userinfo.email",
                                        "https://www.googleapis.com/auth/firebase",
                                        "https://www.googleapis.com/auth/cloud-platform"
                                       }
                                   );
            }

            if (_app == null)
            {
                try
                {
                    _app = FirebaseApp.Create(new AppOptions
                    {
                        Credential = _googleCredential,

                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    _app = FirebaseApp.DefaultInstance;
                }
            }
            try
            {
                _auth = FirebaseAuth.GetAuth(_app);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                _auth = FirebaseAuth.DefaultInstance;
            }

        }

        [HttpPost]
        [Route("get")]
        public async Task<FirebaseCustomTokenRespose> Get([FromForm] FirebaseCustomTokenRequest request)
        {
            var uid = request.Uid;
            var pwd = request.Pwd;
            //connect to your db to valid uid, pwd

            if (string.IsNullOrEmpty(uid))
            {
                //should remove line, just for test
                uid = "be52d43d-31cd-44be-bfbd-157352eaabec";
            }

            var token = await CreateCustomTokenAsync(uid, new Dictionary<string, object> { });

            return new FirebaseCustomTokenRespose
            {
                CustomToken = token
            };
        }

        async Task<string> CreateCustomTokenAsync(string userId, Dictionary<string, object> claims)
        {
            claims = claims ?? new Dictionary<string, object>();
            claims["uid"] = userId;
            claims["tenantid"] = userId;
            //any thing you want to check in firebase realtime rule
            return await _auth.CreateCustomTokenAsync(userId, claims);
        }
    }

    public class FirebaseCustomTokenRequest
    {
        public string Uid { get; set; }
        public string Pwd { get; set; }
    }

    public class FirebaseCustomTokenRespose
    {
        public string Message { get; set; }
        public int Status { get; set; }
        public string CustomToken { get; set; }
    }
}
