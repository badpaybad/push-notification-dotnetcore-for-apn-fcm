
# firebase realtime 

![image](https://user-images.githubusercontent.com/6204507/136723724-b166f457-c9e0-49b5-ac07-54ecdfc44270.png)


# setup firebase

                Server side: 
                
                You have to create prj then get json file for FirebaseAdmin , use it to create custom token
                Active Authentication for firebase

                Client side: 
                Create prj for web, android, ios 
                call api login then get custom token, use FirebaseClient lib to do 


 check image: *.png
 
				1.firebase...

### server side FirebaseAdmin

you have to download and save your json file for FirebaseAdmin. Server will use it to create CustomToken

### php laravel check folder: laravel-firebaseadmin

composer require kreait/firebase-php

				routes\api.php


### c# check folder: PushNotification.RealtimeFirebase

	nuget FirebaseAdmin

				FirebaseWithCustomTokenController.cs


# client js or android, ios

check file: PushNotification.RealtimeFirebase\wwwroot\test\index.html

check file : resources\views\welcome.blade.php


# create firebase app to get config and use firebase client lib

check image 1.firebase...

# push-notification-dotnetcore-for-apn-fcm
APN, FCM push notification for c# dotnetcore, HTTP version 2


Just support send single device_token for app mobile

			    ApnsServerEnvironment apnEnv = ApnsServerEnvironment.Sandbox;
                //https://developer.apple.com/library/archive/documentation/NetworkingInternet/Conceptual/RemoteNotificationsPG/CommunicatingwithAPNs.html#//apple_ref/doc/uid/TP40008194-CH11-SW1
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

