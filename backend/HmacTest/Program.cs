using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

string passHash = "d817107cd67af95c55ac881b57ec694388d39db3fab5e219c7662137784aec50";
long accountId = 1855564010;
long userId = -402627319;
long time = 1766802700703;  // From browser capture
long startSecs = 1766802667;
long lastModified = 1766184797;

// Variant 1: Exact browser format (manual JSON)
var json1 = $"{{\"accountId\":{accountId},\"action\":\"getAllData\",\"addressing\":\"all\",\"isAdmin\":\"false\",\"lastModified\":{lastModified},\"service\":\"fleet\",\"startSecs\":{startSecs},\"time\":{time},\"userId\":{userId},\"version\":\"45.24\"}}";
Console.WriteLine($"JSON1: {json1}");
Console.WriteLine($"HMAC1: {GetHmac(json1, passHash)}");

// Variant 2: Maybe HMAC is on just the values concatenated?
var concat = $"{accountId}getAllDataallfalse{lastModified}fleet{startSecs}{time}{userId}45.24";
Console.WriteLine($"\nConcat: {concat}");
Console.WriteLine($"HMAC2: {GetHmac(concat, passHash)}");

// Variant 3: Maybe passHash as hex bytes?
byte[] passHashBytes = new byte[passHash.Length / 2];
for (int i = 0; i < passHashBytes.Length; i++)
    passHashBytes[i] = Convert.ToByte(passHash.Substring(i * 2, 2), 16);

Console.WriteLine($"\nHMAC3 (hex key): {GetHmacBytes(json1, passHashBytes)}");
Console.WriteLine($"HMAC4 (hex key, concat): {GetHmacBytes(concat, passHashBytes)}");

// Expected HMAC from browser capture
Console.WriteLine($"\nExpected: fc9b7013f670a8163a0ec04a66818ceef859ee069fd50674e96047d54f7df838");

static string GetHmac(string message, string key) {
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static string GetHmacBytes(string message, byte[] keyBytes) {
    using var hmac = new HMACSHA256(keyBytes);
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    return Convert.ToHexString(hash).ToLowerInvariant();
}
