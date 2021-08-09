using Newtonsoft.Json;
using PushNotification.ApnFcmHttpV2;
using System;
using System.Threading.Tasks;
using static PushNotification.ApnFcmHttpV2.ApnsConfiguration;

namespace PushNotification.TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ApnsServerEnvironment apnEnv = ApnsServerEnvironment.Sandbox;

            //from your IOs application, devloper have to export to you
            var certData = System.IO.File.ReadAllBytes("certFullPathFile .p12");
            var apnPwd = "your pass here";

            var data = JsonConvert.SerializeObject(new { id= Guid.NewGuid(), value="define any data to app mobile to process", type=1, url="http://..." });

            var n = new { device_id ="your app generated token", title="title in noti lock screen", body= "body in noti lock screen", data=data};

            var appleClient = new APNsClient(new ApnsConfiguration(apnEnv, certData, apnPwd));

            //https://firebase.google.com/docs/cloud-messaging/server
            var fcmKey = "";
            var fcmUrl = "https://fcm.googleapis.com/fcm/send";

            var fcmClient = new FCMsClient(new GcmConfiguration(fcmKey) { GcmUrl = fcmUrl });


            var tempR = await appleClient.Send(n.device_id, n.title, n.body, n.data);

            Console.WriteLine(JsonConvert.SerializeObject(tempR));

            tempR = await fcmClient.Send(n.device_id, n.title, n.body, n.data);

            Console.WriteLine(JsonConvert.SerializeObject(tempR));

            while (true)
            {
                var cmd = Console.ReadLine();

                if (cmd == "q")
                {
                    Environment.Exit(0);
                    return;
                }

                await Task.Delay(1000);
            }
        }
    }
}
