using System;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

public class VnPayLibrary
{
    private readonly SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayCompare());
    private readonly SortedList<string, string> _responseData = new SortedList<string, string>(new VnPayCompare());

    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value)) _requestData.Add(key, value);
    }

    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value)) _responseData.Add(key, value);
    }

    public string GetResponseData(string key) => _responseData.TryGetValue(key, out var ret) ? ret : string.Empty;

    public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
    {
        var data = new StringBuilder();
        foreach (var (key, value) in _requestData.Where(kv => !string.IsNullOrEmpty(kv.Value)))
        {
            data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
        }

        string queryString = data.ToString();
        string rawData = queryString.Remove(queryString.Length - 1);
        string vnpSecureHash = HmacSHA512(vnpHashSecret, rawData);

        return $"{baseUrl}?{queryString}vnp_SecureHash={vnpSecureHash}";
    }

    public bool ValidateSignature(string inputHash, string secretKey)
    {
        var data = new StringBuilder();
        foreach (var (key, value) in _responseData.Where(kv => kv.Key.StartsWith("vnp_") && kv.Key != "vnp_SecureHash"))
        {
            data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
        }

        string rawData = data.ToString().TrimEnd('&');
        string myChecksum = HmacSHA512(secretKey, rawData);
        return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
    }

    private static string HmacSHA512(string key, string inputData)
    {
        var hash = new StringBuilder();
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
        using var hmac = new HMACSHA512(keyBytes);
        byte[] hashValue = hmac.ComputeHash(inputBytes);
        foreach (byte theByte in hashValue) hash.Append(theByte.ToString("x2"));
        return hash.ToString();
    }
}

public class VnPayCompare : IComparer<string>
{
    public int Compare(string? x, string? y) => string.Compare(x, y, StringComparison.Ordinal);
}