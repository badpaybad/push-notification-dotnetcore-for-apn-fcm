<?php

use Illuminate\Http\Request;
use Illuminate\Support\Facades\Route;

use Kreait\Firebase\Factory;
/*
|--------------------------------------------------------------------------
| API Routes
|--------------------------------------------------------------------------
|
| Here is where you can register API routes for your application. These
| routes are loaded by the RouteServiceProvider within a group which
| is assigned the "api" middleware group. Enjoy building your API!
|
*/

Route::middleware('auth:sanctum')->get('/user', function (Request $request) {
    return $request->user();
});


Route::post('/firebase/getcustomtoken', function (Request $request) {

    //dd($request);

    $fileJson =env("FIREBASEADMIN_FILEJSON");
    
    $uid= @$request["uid"];
    #$pwd= @$request["pwd"];
    //check uid pwd in your db
     //should remove line, just for test
    $uid=md5("your uid pwd");
    
    //https://firebase-php.readthedocs.io/en/stable/realtime-database.html
    $factory = (new Factory)->withServiceAccount($fileJson);
    $auth = $factory->createAuth();

    $additionalClaims = [
        'uid' => $uid
    ];
    
    $customToken = $auth->createCustomToken($uid,$additionalClaims);

    return ["customToken"=>$customToken->toString()];
});

